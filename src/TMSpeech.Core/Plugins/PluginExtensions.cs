namespace TMSpeech.Core.Plugins;

static public class PluginExtensions
{
    public static void LoadConfig(this IPlugin plugin, string config)
    {
        plugin.Configuration.Load(config);
    }
}