using NAudio.CoreAudioApi;
using TMSpeech.Core.Plugins;

namespace TMSpeech.AudioSource.Windows;

public class MicrophoneConfigEditor : IPluginConfigEditor
{
    public IReadOnlyList<PluginConfigFormItem> GetFormItems()
    {
        var enumerator = new MMDeviceEnumerator();
        var list = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();

        return new PluginConfigFormItem[]
        {
            new PluginConfigFormItemOption("device", "设备", list.ToDictionary<MMDevice, object, string>(
                x => x.ID, x => x.FriendlyName
            )),
        };
    }

    public event EventHandler<EventArgs>? FormItemsUpdated;

    private string _deviceID = "";

    public IReadOnlyDictionary<string, object> GetAll()
    {
        return new Dictionary<string, object>
        {
            { "device", _deviceID }
        };
    }

    public void SetValue(string key, object value)
    {
        if (key == "device")
        {
            _deviceID = value.ToString();
        }
    }

    public object GetValue(string key)
    {
        return _deviceID;
    }

    public event EventHandler<EventArgs>? ValueUpdated;

    public string GenerateConfig()
    {
        return _deviceID;
    }

    public void LoadConfigString(string config)
    {
        _deviceID = config;
    }
}