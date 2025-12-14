using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TMSpeech.Core;
using TMSpeech.Core.Utils;

namespace TMSpeech.GUI
{
    internal static class DefaultConfig
    {
        public static Dictionary<string, object> GenerateConfig()
        {
            var ret = GeneralConfigTypes.DefaultConfig
                .Union(AppearanceConfigTypes.DefaultConfig)
                .Union(NotificationConfigTypes.DefaultConfig)
                .ToDictionary(x => x.Key, x => x.Value);
            ret["audio.source"] = "TMSpeech:AudioSource:Windows!F32B7F03-7030-4960-A8DF-96377C8B5FDD";
            ret["recognizer.source"] = "TMSpeech:Recognizer:SherpaOnnx!3002EE6C-9770-419F-A745-E3148747AF4C";
            var fonts = FontManager.Current.SystemFonts.ToList();
            if (fonts.Any(x => x.Name == "黑体")) ret["appearance.FontFamily"] = "黑体";

            // Read default config and merge.
            var defaultConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "default_config.json");
            try
            {
                if (File.Exists(defaultConfig))
                {
                    var config = File.ReadAllText(defaultConfig);
                    var value = JsonSerializer.Deserialize<Dictionary<string, object>>(config,
                        new JsonSerializerOptions
                        {
                            Converters = { new SystemObjectNewtonsoftCompatibleConverter() }
                        });
                    if (value != null)
                    {
                        // merge config with default config
                        for (var i = 0; i < value.Count; i++)
                        {
                            ret[value.ElementAt(i).Key] = value.ElementAt(i).Value;
                        }
                    }
                }
            }
            catch (FileNotFoundException)
            {
                // ok
            }
            catch (Exception ex)
            {
                TMSpeech.Core.Services.Notification.NotificationManager.Instance.Notify(
                    $"配置加载失败：{defaultConfig}\n{ex.Message}\n{ex.StackTrace}",
                    "配置保存失败",
                    TMSpeech.Core.Services.Notification.NotificationType.Error);
                System.Diagnostics.Debug.WriteLine($"配置加载失败：{defaultConfig}\n{ex.Message}\n{ex.StackTrace}");
            }

            return ret;
        }
    }
}