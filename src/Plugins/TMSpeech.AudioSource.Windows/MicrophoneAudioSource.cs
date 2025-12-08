using NAudio.CoreAudioApi;
using NAudio.Wave;
using TMSpeech.Core.Plugins;

namespace TMSpeech.AudioSource.Windows;

public class MicrophoneAudioSource : IAudioSource
{
    public string GUID => "3746756F-07D8-4972-BBF7-C443DF1E7E24";
    public string Name => "Windows 麦克风输入";
    public string Description => "从系统录制输入";
    public string Version => "0.0.1";
    public string SupportVersion => "any";
    public string Author => "Built-in";
    public string Url => "";
    public string License => "MIT License";
    public string Note => "";

    public IPluginConfigEditor CreateConfigEditor()
    {
        return new MicrophoneConfigEditor();
    }

    private string _deviceID = "";

    public void LoadConfig(string config)
    {
        _deviceID = config;
    }

    public bool Available => OperatingSystem.IsWindows();

    private WasapiCapture? _waveIn;

    public void Init()
    {
    }

    public void Destroy()
    {
    }

    public void Start()
    {
        try
        {
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDevice(_deviceID);
            if (device == null)
            {
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            }

            _waveIn = new WasapiCapture(device)
            {
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(16000, 1)
            };
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
        catch (Exception ex)
        {
            ExceptionOccured?.Invoke(this, new Exception("启动麦克风失败", ex));
            throw;
        }
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