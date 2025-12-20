using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using TMSpeech.AudioSource.Windows;

namespace TMSpeech.AudioSource.Windows;

/// <summary>
/// 用于捕获指定进程音频的 WASAPI Capture 类
/// </summary>
public class ProcessWasapiCapture : IWaveIn
{
    private const long ReftimesPerSec = 10000000;
    private const long ReftimesPerMillisec = 10000;
    private const int FALLBACK_BUFFER_LENGTH = 10000;
    private const string VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK = "VAD\\Process_Loopback";

    private volatile CaptureState captureState;
    private byte[] recordBuffer;
    private Thread captureThread;
    private AudioClient audioClient;
    private int bytesPerFrame;
    private WaveFormat waveFormat;
    private bool initialized;
    private readonly SynchronizationContext syncContext;
    private readonly bool isUsingEventSync;
    private EventWaitHandle frameEventWaitHandle;
    private readonly int audioBufferMillisecondsLength;
    private AudioClientStreamFlags audioClientStreamFlags;

    /// <summary>
    /// Indicates recorded data is available
    /// </summary>
    public event EventHandler<WaveInEventArgs> DataAvailable;

    /// <summary>
    /// Indicates that all recorded data has now been received.
    /// </summary>
    public event EventHandler<StoppedEventArgs> RecordingStopped;

    /// <summary>
    /// Current Capturing State
    /// </summary>
    public CaptureState CaptureState => captureState;

    /// <summary>
    /// Capturing wave format
    /// </summary>
    public virtual WaveFormat WaveFormat
    {
        get
        {
            return waveFormat?.AsStandardWaveFormat() ?? new WaveFormat();
        }
        set { waveFormat = value; }
    }

    /// <summary>
    /// Share Mode - set before calling StartRecording
    /// </summary>
    public AudioClientShareMode ShareMode { get; set; }

    private ProcessWasapiCapture(AudioClient audioClient, bool useEventSync, int audioBufferMillisecondsLength)
    {
        syncContext = SynchronizationContext.Current;
        this.audioClient = audioClient;
        ShareMode = AudioClientShareMode.Shared;
        this.isUsingEventSync = useEventSync;
        this.audioBufferMillisecondsLength = audioBufferMillisecondsLength;
        // enable auto-convert PCM
        this.audioClientStreamFlags = AudioClientStreamFlags.AutoConvertPcm | AudioClientStreamFlags.SrcDefaultQuality;
    }

    /// <summary>
    /// 创建进程音频捕获（包含子进程）
    /// </summary>
    public static async Task<ProcessWasapiCapture> CreateForProcessCaptureAsync(int processId)
    {
        // 验证进程ID
        if (processId <= 0)
        {
            throw new ArgumentException("Invalid process ID");
        }

        // 检查进程是否存在
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(processId);
            if (process.HasExited)
            {
                throw new InvalidOperationException($"Process {processId} has already exited");
            }
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException($"Process {processId} does not exist");
        }

        // 创建音频客户端激活参数
        var activationParams = new AudioClientActivationParams
        {
            ActivationType = AudioClientActivationType.ProcessLoopback,
            ProcessLoopbackParams = new AudioClientProcessLoopbackParams
            {
                TargetProcessId = (uint)processId,
                ProcessLoopbackMode = ProcessLoopbackMode.IncludeTargetProcessTree
            }
        };

        var hBlobData = GCHandle.Alloc(activationParams, GCHandleType.Pinned);
        try
        {
            var data = hBlobData.AddrOfPinnedObject();
            var activateParams = new PropVariant
            {
                vt = (short)VarEnum.VT_BLOB,
                blobVal = new Blob
                {
                    Length = Marshal.SizeOf(activationParams),
                    Data = data
                }
            };

            ProcessWasapiCapture capture = null;

            // 创建完成处理器
            var icbh = new ProcessActivationCompletionHandler(audioClientInterface =>
            {
                // 使用 IAudioClient 接口创建 AudioClient 实例
                var audioClient = new AudioClient(audioClientInterface);
                capture = new ProcessWasapiCapture(audioClient, true, 100);
                capture.audioClientStreamFlags |= AudioClientStreamFlags.Loopback;
                capture.WaveFormat = new WaveFormat(); // ask for capture at 44.1, stereo 16 bit
            });

            var hActivateParams = GCHandle.Alloc(activateParams, GCHandleType.Pinned);
            try
            {
                NativeMethods.ActivateAudioInterfaceAsync(
                    VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK,
                    typeof(IAudioClient).GUID,
                    hActivateParams.AddrOfPinnedObject(),
                    icbh,
                    out var activationOperation);

                // 直接等待完成处理器的任务
                await icbh;
                return capture;
            }
            finally
            {
                hActivateParams.Free();
            }
        }
        finally
        {
            hBlobData.Free();
        }
    }

    /// <summary>
    /// To allow overrides to specify different flags (e.g. loopback)
    /// </summary>
    protected virtual AudioClientStreamFlags GetAudioClientStreamFlags()
    {
        return audioClientStreamFlags;
    }

    private void InitializeCaptureDevice()
    {
        if (initialized)
            return;

        var requestedDuration = ReftimesPerMillisec * audioBufferMillisecondsLength;

        var streamFlags = GetAudioClientStreamFlags();

        // If using EventSync, setup is specific with shareMode
        if (isUsingEventSync)
        {
            // Init Shared or Exclusive
            if (ShareMode == AudioClientShareMode.Shared)
            {
                // With EventCallBack and Shared, both latencies must be set to 0
                audioClient.Initialize(ShareMode, AudioClientStreamFlags.EventCallback | streamFlags, requestedDuration, 0,
                    waveFormat, Guid.Empty);
            }
            else
            {
                // With EventCallBack and Exclusive, both latencies must equals
                audioClient.Initialize(ShareMode, AudioClientStreamFlags.EventCallback | streamFlags, requestedDuration, requestedDuration,
                                    waveFormat, Guid.Empty);
            }

            // Create the Wait Event Handle
            frameEventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            audioClient.SetEventHandle(frameEventWaitHandle.SafeWaitHandle.DangerousGetHandle());
        }
        else
        {
            // Normal setup for both sharedMode
            audioClient.Initialize(ShareMode,
            streamFlags,
            requestedDuration,
            0,
            waveFormat,
            Guid.Empty);
        }

        var bufferFrameCount = audioClient.BufferSize;
        bytesPerFrame = waveFormat.Channels * waveFormat.BitsPerSample / 8;
        var bufferSize = bufferFrameCount * bytesPerFrame;

        if (bufferSize < 1)
        {
            var fallbackSize = FALLBACK_BUFFER_LENGTH * bytesPerFrame;
            bufferSize = fallbackSize;
        }

        recordBuffer = new byte[bufferSize];

        initialized = true;
    }

    /// <summary>
    /// Start Capturing
    /// </summary>
    public void StartRecording()
    {
        if (captureState != CaptureState.Stopped)
        {
            throw new InvalidOperationException("Previous recording still in progress");
        }
        captureState = CaptureState.Starting;
        InitializeCaptureDevice();
        captureThread = new Thread(() => CaptureThread(audioClient));
        captureThread.Start();
    }

    /// <summary>
    /// Stop Capturing (requests a stop, wait for RecordingStopped event to know it has finished)
    /// </summary>
    public void StopRecording()
    {
        if (captureState != CaptureState.Stopped)
            captureState = CaptureState.Stopping;
    }

    private void CaptureThread(AudioClient client)
    {
        Exception exception = null;
        try
        {
            DoRecording(client);
        }
        catch (Exception e)
        {
            exception = e;
        }
        finally
        {
            client.Stop();
            // don't dispose - the AudioClient only gets disposed when ProcessWasapiCapture is disposed
        }
        captureThread = null;
        captureState = CaptureState.Stopped;
        RaiseRecordingStopped(exception);
    }

    private void DoRecording(AudioClient client)
    {
        var bufferFrameCount = client.BufferSize;
        if (bufferFrameCount < 1) // BufferSize is faulted
            bufferFrameCount = FALLBACK_BUFFER_LENGTH;

        // Calculate the actual duration of the allocated buffer.
        var actualDuration = (long)((double)ReftimesPerSec *
                         bufferFrameCount / waveFormat.SampleRate);
        var sleepMilliseconds = (int)(actualDuration / ReftimesPerMillisec / 2);
        var waitMilliseconds = (int)(3 * actualDuration / ReftimesPerMillisec);

        var capture = client.AudioCaptureClient;
        client.Start();
        // avoid race condition where we stop immediately after starting
        if (captureState == CaptureState.Starting)
        {
            captureState = CaptureState.Capturing;
        }
        while (captureState == CaptureState.Capturing)
        {
            if (isUsingEventSync)
            {
                frameEventWaitHandle.WaitOne(waitMilliseconds, false);
            }
            else
            {
                Thread.Sleep(sleepMilliseconds);
            }
            if (captureState != CaptureState.Capturing)
                break;

            // If still recording
            ReadNextPacket(capture);
        }
    }

    private void RaiseRecordingStopped(Exception e)
    {
        var handler = RecordingStopped;
        if (handler == null) return;
        if (syncContext == null)
        {
            handler(this, new StoppedEventArgs(e));
        }
        else
        {
            syncContext.Post(state => handler(this, new StoppedEventArgs(e)), null);
        }
    }

    private void ReadNextPacket(AudioCaptureClient capture)
    {
        var packetSize = capture.GetNextPacketSize();
        var recordBufferOffset = 0;

        while (packetSize != 0)
        {
            var buffer = capture.GetBuffer(out var framesAvailable, out var flags);

            var bytesAvailable = framesAvailable * bytesPerFrame;

            // apparently it is sometimes possible to read more frames than we were expecting?
            // fix suggested by Michael Feld:
            var spaceRemaining = Math.Max(0, recordBuffer.Length - recordBufferOffset);
            if (spaceRemaining < bytesAvailable && recordBufferOffset > 0)
            {
                DataAvailable?.Invoke(this, new WaveInEventArgs(recordBuffer, recordBufferOffset));
                recordBufferOffset = 0;
            }

            // if not silence...
            if ((flags & AudioClientBufferFlags.Silent) != AudioClientBufferFlags.Silent)
            {
                Marshal.Copy(buffer, recordBuffer, recordBufferOffset, bytesAvailable);
            }
            else
            {
                Array.Clear(recordBuffer, recordBufferOffset, bytesAvailable);
            }
            recordBufferOffset += bytesAvailable;
            capture.ReleaseBuffer(framesAvailable);
            packetSize = capture.GetNextPacketSize();
        }
        DataAvailable?.Invoke(this, new WaveInEventArgs(recordBuffer, recordBufferOffset));
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
        StopRecording();
        if (captureThread != null)
        {
            captureThread.Join();
            captureThread = null;
        }
        if (audioClient != null)
        {
            audioClient.Dispose();
            audioClient = null;
        }
        GC.SuppressFinalize(this);
    }
}