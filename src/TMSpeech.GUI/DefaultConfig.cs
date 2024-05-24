using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMSpeech.GUI
{
    internal static class DefaultConfig
    {
        public static Dictionary<string, object> GenerateConfig()
        {

            var ret = new Dictionary<string, object> {
                { "general.Language", "zh-cn"},
                { "appearance.ShadowColor", 0xFF000000 },
                { "appearance.ShadowSize", 10 },
                { "appearance.FontFamily", "Arial" },
                { "appearance.FontSize", 48 },
                { "appearance.FontColor", 0xFFFFFFFF },
                { "appearance.MouseHover", 0x2709A9FF },
                { "appearance.TextAlign", 0 }
            };

            var fonts = FontManager.Current.SystemFonts.ToList();
            if (fonts.Any(x => x.Name == "黑体")) ret["appearance.FontFamily"] = "黑体";
            return ret;
        }
    }
}
