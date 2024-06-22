using System.Text.Json;
using TMSpeech.Core.Utils;

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
        return ChangedKeys?.Any(u => u == section || ConfigManager.IsInSection(u, section)) ?? false;
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
    public abstract IReadOnlyDictionary<string, object> GetAll();

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

    public abstract void Load();
    protected abstract void Save();
}

class LocalConfigManagerImpl : ConfigManager
{
    public LocalConfigManagerImpl(Dictionary<string, object> defaultConfig) : base()
    {
        _config = defaultConfig;
        Load();
    }

    private string _userDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TMSpeech");

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

    public override void Apply<T>(string key, T value)
    {
        BatchApply(new Dictionary<string, object> { { key, value } });
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
        Save();
    }

    public override void DeleteAndApply<T>(string key)
    {
        _config.Remove(key);
        OnConfigChange(new ConfigChangedEventArgs(new List<string> { key }));
        Save();
    }

    public override T Get<T>(string key)
    {
        if (!_config.ContainsKey(key)) return default(T);
        return (T)Convert.ChangeType(_config[key], typeof(T));
    }

    public override IReadOnlyDictionary<string, object> GetAll()
    {
        return _config;
    }

    private string ConfigFile => Path.Combine(UserDataDir, "config.json");

    public override void Load()
    {

        if (!Directory.Exists(_userDataDir)) Directory.CreateDirectory(_userDataDir);
        try
        {
            var config = File.ReadAllText(ConfigFile);
            var value = JsonSerializer.Deserialize<Dictionary<string, object>>(config,
                new JsonSerializerOptions
                {
                    Converters = { new SystemObjectNewtonsoftCompatibleConverter() }
                });
            if (value == null) return;

            _config = value;
        }
        catch (FileNotFoundException)
        {
        }

        OnConfigChange(new ConfigChangedEventArgs(null, ConfigChangedEventArgs.ChangeType.All));
    }

    protected override void Save()
    {
        if (!Directory.Exists(_userDataDir)) Directory.CreateDirectory(_userDataDir);
        var text = JsonSerializer.Serialize(_config, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        });
        File.WriteAllText(ConfigFile, text);
    }
}

public static class ConfigManagerFactory
{
    private static Dictionary<string, object> _defaultConfig;

    public static void Init(Dictionary<string, object> defaultConfig)
    {
        if (_instance.IsValueCreated) throw new Exception("ConfigManagerFactory already initialized");
        _defaultConfig = defaultConfig;
    }

    private static Lazy<ConfigManager> _instance = new(() => new LocalConfigManagerImpl(_defaultConfig));
    public static ConfigManager Instance
    {
        get
        {
            if (_defaultConfig == null) throw new Exception("Default config does not exists, call Init() first");
            return _instance.Value;
        }
    }
}