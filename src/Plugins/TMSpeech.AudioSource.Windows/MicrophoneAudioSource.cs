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
            DataAvailable?.Invoke(sender, data.Buffer[..data.BytesRecorded]);
        };
        _waveIn.StartRecording();

        StatusChanged?.Invoke(this, SourceStatus.Ready);
    }

    public void Stop()
    {
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;

        StatusChanged?.Invoke(this, SourceStatus.Unavailable);
    }

    public event EventHandler<Exception>? ExceptionOccured;

    public event EventHandler<SourceStatus>? StatusChanged;
    public event EventHandler<byte[]>? DataAvailable;
}