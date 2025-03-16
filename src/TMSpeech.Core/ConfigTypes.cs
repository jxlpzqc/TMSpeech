namespace TMSpeech.Core;

public static class GeneralConfigTypes
{
    public const string SectionName = "general";

    public const string Language = "general.Language";
    public const string LaunchOnStartup = "general.LaunchOnStartup";
    public const string StartOnLaunch = "general.StartOnLaunch";
    public const string AutoUpdate = "general.AutoUpdate";
    public const string ResultLogPath = "general.ResultLogPath";
    public const string MainWindowLocation = "general.MainWindowLocation";


    private static Dictionary<string, object> _defaultConfig => new()
    {
        { Language, "zh-cn" },
        { LaunchOnStartup, false },
        { StartOnLaunch, true },
        { AutoUpdate, true },
        { ResultLogPath, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TMSpeechLogs") },
        { MainWindowLocation, new List<int>() }
    };

    public static Dictionary<string, object> DefaultConfig => _defaultConfig;
}

public static class AppearanceConfigTypes
{
    public const string SectionName = "appearance";

    public const string ShadowColor = "appearance.ShadowColor";
    public const string ShadowSize = "appearance.ShadowSize";
    public const string FontFamily = "appearance.FontFamily";
    public const string FontSize = "appearance.FontSize";
    public const string FontColor = "appearance.FontColor";
    public const string MouseHover = "appearance.MouseHover";
    public const string TextAlign = "appearance.TextAlign";
    public const string BackgroundColor = "appearance.BackgroundColor";

    public static class TextAlignEnum
    {
        public const int Left = 0;
        public const int Center = 1;
        public const int Right = 2;
        public const int Justify = 3;
    }


    private static Dictionary<string, object> _defaultConfig => new()
    {
        { ShadowColor, 0xFF000000 },
        { ShadowSize, 10 },
        { FontFamily, "Arial" },
        { FontSize, 48 },
        { FontColor, 0xFFFFFFFF },
        { MouseHover, 0x2709A9FF },
        { TextAlign, TextAlignEnum.Left },
        { BackgroundColor, 0x00000000 }
    };

    public static Dictionary<string, object> DefaultConfig => _defaultConfig;
}

public static class NotificationConfigTypes
{
    public const string SectionName = "notification";

    public const string NotificationType = "notification.NotificationType";
    public const string SensitiveWords = "notification.SensitiveWords";
    public const string HasShownLockUsage = "notification.ShownLockUsage";

    public static class NotificationTypeEnum
    {
        public const int None = 0;
        public const int System = 1;
        public const int Custom = 2;
    }

    private static Dictionary<string, object> _defaultConfig => new()
    {
        { NotificationType, NotificationTypeEnum.System },
        { SensitiveWords, "" }
    };

    public static Dictionary<string, object> DefaultConfig => _defaultConfig;
}

public static class AudioSourceConfigTypes
{
    public const string SectionName = "audio";

    public const string AudioSource = "audio.source";

    public static string GetPluginConfigKey(string pluginId)
    {
        return $"plugin.{pluginId}.config";
    }
}

public static class RecognizerConfigTypes
{
    public const string SectionName = "recognizer";

    public const string Recognizer = "recognizer.source";

    public static string GetPluginConfigKey(string pluginId)
    {
        return $"plugin.{pluginId}.config";
    }
}