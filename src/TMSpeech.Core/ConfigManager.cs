using System.Text.Json;

namespace TMSpeech.Core;

public class ConfigChangedEventArgs : EventArgs
{
    public ICollection<string>? ChangedKeys { get; }

    public enum ChangeType
    {
        Partial,
        All
    }

    public ChangeType Type { get; }

    public bool Contains(string section)
    {
        if (Type == ChangeType.All) return true;
        return ChangedKeys?.Any(u => ConfigManager.IsInSection(u, section)) ?? false;
    }

    public ConfigChangedEventArgs(ICollection<string>? keys, ChangeType type = ChangeType.Partial) : base()
    {
        ChangedKeys = keys;
        Type = type;
    }
}

public abstract class ConfigManager
{
    public abstract string UserDataDir { get; set; }

    public abstract void Apply<T>(string key, T value);
    public abstract void BatchApply(IDictionary<string, object> config);
    public abstract void DeleteAndApply<T>(string key);

    public abstract T Get<T>(string key);

    public event EventHandler<ConfigChangedEventArgs> ConfigChanged;

    public void OnConfigChange(ConfigChangedEventArgs arg)
    {
        ConfigChanged?.Invoke(this, arg);
    }

    public const string ConfigKeySeparator = ".";

    public static bool IsInSection(string key, string section)
    {
        return key.Contains(section + ConfigKeySeparator);
    }

    public static IList<string> KeyToList(string key)
    {
        return key.Split(ConfigKeySeparator);
    }

    public static string ListToKey(IList<string> list)
    {
        return string.Join(ConfigKeySeparator, list);
    }

    public abstract void Reset();
    public abstract void Load();
    public abstract void Save();
}

class LocalConfigManagerImpl : ConfigManager
{
    private string _userDataDir = "";

    public override string UserDataDir
    {
        get => _userDataDir;
        set
        {
            _userDataDir = value;
            Load();
        }
    }

    private Dictionary<string, object>? _config = new();
    private Dictionary<string, object>? _configBackup = new();

    public override void Apply<T>(string key, T value)
    {
        _config[key] = value;
        OnConfigChange(new ConfigChangedEventArgs(new List<string> { key }));
    }

    public override void BatchApply(IDictionary<string, object> config)
    {
        List<string> changed = new();
        foreach (var c in config)
        {
            _config[c.Key] = c.Value;
            changed.Add(c.Key);
        }

        OnConfigChange(new ConfigChangedEventArgs(changed));
    }

    public override void DeleteAndApply<T>(string key)
    {
        _config.Remove(key);
        OnConfigChange(new ConfigChangedEventArgs(new List<string> { key }));
    }

    public override T Get<T>(string key)
    {
        if (!_config.ContainsKey(key)) return default(T);
        return (T)_config[key];
    }

    public override void Reset()
    {
        _config = new Dictionary<string, object>(_configBackup);
        OnConfigChange(new ConfigChangedEventArgs(null, ConfigChangedEventArgs.ChangeType.All));
    }

    private string ConfigFile => Path.Combine(UserDataDir, "config.json");

    public override void Load()
    {
        var config = File.ReadAllText(ConfigFile);
        var value = JsonSerializer.Deserialize<Dictionary<string, object>>(config);
        if (value == null) return;

        _config = value;
        _configBackup = new Dictionary<string, object>(_config);

        OnConfigChange(new ConfigChangedEventArgs(null, ConfigChangedEventArgs.ChangeType.All));
    }

    public override void Save()
    {
        var text = JsonSerializer.Serialize(_config);
        File.WriteAllText(ConfigFile, text);
    }
}