using System.Text.Json;
using TMSpeech.Core.Plugins;

namespace TMSpeech.Recognizer.Command;

public class CommandRecognizerConfigEditor : IPluginConfigEditor
{
    private readonly Dictionary<string, object> _values = new();
    private readonly List<PluginConfigFormItem> _formItems = new();

    public event EventHandler<EventArgs>? FormItemsUpdated;
    public event EventHandler<EventArgs>? ValueUpdated;

    public CommandRecognizerConfigEditor()
    {
        _values["Command"] = "";
        _values["Arguments"] = "";
        _values["WorkingDirectory"] = "";

        _formItems.Add(new PluginConfigFormItemFile
        {
            Key = "Command",
            Name = "命令路径",
            Type = PluginConfigFormItemFileType.File
        });

        _formItems.Add(new PluginConfigFormItemText
        {
            Key = "Arguments",
            Name = "命令参数"
        });

        _formItems.Add(new PluginConfigFormItemFile
        {
            Key = "WorkingDirectory",
            Name = "工作目录",
            Type = PluginConfigFormItemFileType.Folder
        });
    }

    public IReadOnlyList<PluginConfigFormItem> GetFormItems()
    {
        return _formItems.AsReadOnly();
    }

    public IReadOnlyDictionary<string, object> GetAll()
    {
        return _values;
    }

    public void SetValue(string key, object value)
    {
        _values[key] = value;
        ValueUpdated?.Invoke(this, EventArgs.Empty);
    }

    public object GetValue(string key)
    {
        return _values.TryGetValue(key, out var value) ? value : "";
    }

    public string GenerateConfig()
    {
        var config = new CommandRecognizerConfig
        {
            Command = _values.TryGetValue("Command", out var cmd) ? cmd?.ToString() ?? "" : "",
            Arguments = _values.TryGetValue("Arguments", out var args) ? args?.ToString() ?? "" : "",
            WorkingDirectory = _values.TryGetValue("WorkingDirectory", out var wd) ? wd?.ToString() ?? "" : ""
        };

        return JsonSerializer.Serialize(config);
    }

    public void LoadConfigString(string config)
    {
        if (string.IsNullOrEmpty(config)) return;

        try
        {
            var cfg = JsonSerializer.Deserialize<CommandRecognizerConfig>(config);
            if (cfg != null)
            {
                _values["Command"] = cfg.Command;
                _values["Arguments"] = cfg.Arguments;
                _values["WorkingDirectory"] = cfg.WorkingDirectory;
            }
        }
        catch
        {
            // 加载失败，使用默认值
        }
    }
}
