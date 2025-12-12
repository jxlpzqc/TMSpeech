using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMSpeech.Core;

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
            ret["recognizer.source"] = "TMSpeech:Recognizer:SherpaNcnn!94C23641-CBE0-42B6-9654-82DA42D519F3";
            var fonts = FontManager.Current.SystemFonts.ToList();
            if (fonts.Any(x => x.Name == "黑体")) ret["appearance.FontFamily"] = "黑体";
            return ret;
        }
    }
}