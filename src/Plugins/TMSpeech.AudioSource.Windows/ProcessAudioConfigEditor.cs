using System.Diagnostics;
using TMSpeech.Core.Plugins;

namespace TMSpeech.AudioSource.Windows;

public class ProcessAudioConfigEditor : IPluginConfigEditor
{
    public IReadOnlyList<PluginConfigFormItem> GetFormItems()
    {
        // 获取当前会话ID
        var currentSessionId = Process.GetCurrentProcess().SessionId;

        // 获取当前会话的进程，优先显示有窗口标题的进程
        var processes = Process.GetProcesses()
            // 只获取有名称且在当前会话的进程
            .Where(p => !string.IsNullOrEmpty(p.ProcessName) && p.SessionId == currentSessionId)
            // 优先显示有窗口标题的进程
            .OrderByDescending(p => !string.IsNullOrEmpty(p.MainWindowTitle))
            .ThenBy(p => p.ProcessName)
            .Take(100)
            .ToList();

        // 创建进程选项字典
        var processOptions = processes.ToDictionary(
            p => (object)p.Id,
            p => $"{p.ProcessName} ({p.Id}) {(string.IsNullOrEmpty(p.MainWindowTitle) ? "" : "- " + p.MainWindowTitle)}"
        );

        return new PluginConfigFormItem[]
        {
            new PluginConfigFormItemOption("processId", "选择进程", processOptions),
            // 注意：选择此进程后会自动捕获其所有子进程
        };
    }

    public event EventHandler<EventArgs>? FormItemsUpdated;

    private int _processId = 0;

    public IReadOnlyDictionary<string, object> GetAll()
    {
        return new Dictionary<string, object>
        {
            { "processId", _processId }
        };
    }

    public void SetValue(string key, object value)
    {
        switch (key)
        {
            case "processId":
                if (int.TryParse(value.ToString(), out int processId))
                {
                    _processId = processId;
                }
                break;
        }
    }

    public object GetValue(string key)
    {
        return key switch
        {
            "processId" => _processId,
            _ => null
        };
    }

    public event EventHandler<EventArgs>? ValueUpdated;

    public string GenerateConfig()
    {
        // 简单地将进程ID作为配置字符串
        return $"{_processId}";
    }

    public void LoadConfigString(string config)
    {
        if (int.TryParse(config, out int processId))
        {
            _processId = processId;
        }
    }
}