using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TMSpeech.Core.Plugins;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace TMSpeech.AudioSource.Windows;

public class ProcessAudioSource : IAudioSource
{
    public string GUID => "CE70909A-DBFC-4FF2-8059-30DDCFBDDF78";
    public string Name => "Windows 进程音频";
    public string Description => "录制指定进程的音频输出";
    public string Version => "0.0.1";
    public string SupportVersion => "any";
    public string Author => "Built-in";
    public string Url => "";
    public string License => "MIT License";
    public string Note => "";

    public IPluginConfigEditor CreateConfigEditor()
    {
        return new ProcessAudioConfigEditor();
    }

    private int _processId = 0;

    public void LoadConfig(string config)
    {
        if (int.TryParse(config, out int processId))
        {
            _processId = processId;
        }
    }

    public bool Available => OperatingSystem.IsWindows();

    private ProcessWasapiCapture? _waveIn;

    public void Init()
    {
    }

    public void Destroy()
    {
    }

    public async void Start()
    {
        if (_processId == 0)
        {
            ExceptionOccured?.Invoke(this, new ArgumentException("未指定进程ID，请先选择要捕获的进程。"));
            StatusChanged?.Invoke(this, SourceStatus.Unavailable);
            return;
        }

            // 检查进程是否存在
            try
            {
                var process = Process.GetProcessById(_processId);
                if (process.HasExited)
                {
                    ExceptionOccured?.Invoke(this, new InvalidOperationException($"所选进程 {_processId} 已经退出，请重新选择一个正在运行的进程。"));
                    StatusChanged?.Invoke(this, SourceStatus.Unavailable);
                    return;
                }
            }
            catch (ArgumentException)
            {
                ExceptionOccured?.Invoke(this, new InvalidOperationException($"进程 {_processId} 不存在，请重新选择一个有效的进程。"));
                StatusChanged?.Invoke(this, SourceStatus.Unavailable);
                return;
            }

            // 创建进程音频捕获（包含子进程）
            try
            {
                _waveIn = await ProcessWasapiCapture.CreateForProcessCaptureAsync(_processId);
            }
            catch (Exception ex)
            {
                ExceptionOccured?.Invoke(this, new Exception($"无法捕获进程 {_processId} 的音频：{ex.Message}", ex));
                StatusChanged?.Invoke(this, SourceStatus.Unavailable);
                return;
            }

            // 设置音频格式为16kHz单声道IEEE Float
            _waveIn.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(16000, 1);

            _waveIn.DataAvailable += (sender, data) =>
            {
                try
                {
                    DataAvailable?.Invoke(sender, data.Buffer[..data.BytesRecorded]);
                }
                catch (Exception ex)
                {
                    ExceptionOccured?.Invoke(this, new Exception("音频数据处理失败", ex));
                }
            };

            _waveIn.RecordingStopped += (sender, args) =>
            {
                if (args.Exception != null)
                {
                    ExceptionOccured?.Invoke(this, new Exception("录音停止异常", args.Exception));
                }
            };

            _waveIn.StartRecording();

            StatusChanged?.Invoke(this, SourceStatus.Ready);
    }

    public void Stop()
    {
        try
        {
            _waveIn?.StopRecording();
        }
        catch (Exception ex)
        {
            // 停止录音失败，记录但继续清理
            System.Diagnostics.Debug.WriteLine($"停止录音失败: {ex.Message}");
        }

        try
        {
            _waveIn?.Dispose();
        }
        catch (Exception ex)
        {
            // 资源释放失败，记录但继续
            System.Diagnostics.Debug.WriteLine($"释放音频资源失败: {ex.Message}");
        }

        _waveIn = null;
        StatusChanged?.Invoke(this, SourceStatus.Unavailable);
    }

    public event EventHandler<Exception>? ExceptionOccured;
    public event EventHandler<SourceStatus>? StatusChanged;
    public event EventHandler<byte[]>? DataAvailable;
}