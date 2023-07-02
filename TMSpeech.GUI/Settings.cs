using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Media;

namespace TMSpeech.GUI
{
    class ApplyToResource : Attribute { }

    [Serializable]
    class Settings : INotifyPropertyChanged
    {
        [JsonIgnore]
        [ApplyToResource]
        public SolidColorBrush FontBrush => ConvertToBrush(FontColor);


        [JsonIgnore]
        public Color FontColor { get; set; }

        public string FontColorStr
        {
            get => FontColor.ToString();
            set
            {
                FontColor = (Color)ColorConverter.ConvertFromString(value);
            }
        }

        private SolidColorBrush ConvertToBrush(Color c)
        {
            return new SolidColorBrush(c);
        }

        [JsonIgnore]
        [ApplyToResource]
        public SolidColorBrush StrokeBrush => ConvertToBrush(_strokeColor);

        private Color _strokeColor;

        [JsonIgnore]
        public Color StrokeColor
        {
            get
            {
                return _strokeColor;
            }
            set
            {
                _strokeColor = value;
                NotifyChange(x => x.StrokeColor);
                NotifyChange(x => x.StrokeBrush);
            }
        }

        public string StrokeColorStr
        {
            get => StrokeColor.ToString();
            set
            {
                StrokeColor = (Color)ColorConverter.ConvertFromString(value);
            }
        }


        [JsonIgnore]
        [ApplyToResource]
        public SolidColorBrush HoverBgBrush => ConvertToBrush(_hoverbgColor);

        private Color _hoverbgColor;

        [JsonIgnore]
        public Color HoverBgColor
        {
            get
            {
                return _hoverbgColor;
            }
            set
            {
                _hoverbgColor = value;
                NotifyChange(x => x.HoverBgColor);
                NotifyChange(x => x.HoverBgBrush);
            }
        }

        public string HoverBgColorStr
        {
            get => HoverBgColor.ToString();
            set
            {
                HoverBgColor = (Color)ColorConverter.ConvertFromString(value);
            }
        }


        [JsonIgnore]
        [ApplyToResource]
        public SolidColorBrush ListBgBrush => ConvertToBrush(_listBgColor);

        private Color _listBgColor;

        [JsonIgnore]
        public Color ListBgColor
        {
            get
            {
                return _listBgColor;
            }
            set
            {
                _listBgColor = value;
                NotifyChange(x => x.ListBgColor);
                NotifyChange(x => x.ListBgBrush);
            }
        }

        public string ListBgColorStr
        {
            get => ListBgColor.ToString();
            set
            {
                ListBgColor = (Color)ColorConverter.ConvertFromString(value);
            }
        }


        private double _fontSize;

        [ApplyToResource]
        public double FontSize
        {
            get => _fontSize;
            set
            {
                _fontSize = value;
                NotifyChange(x => x.FontSize);
            }
        }

        private double _strokeSize;

        [ApplyToResource]
        public double StrokeSize
        {
            get => _strokeSize;
            set
            {
                _strokeSize = value;
                NotifyChange(x => x.StrokeSize);
            }
        }

        private string _fontFamilyStr;

        public string FontFamilyStr
        {
            get => _fontFamilyStr;
            set
            {
                _fontFamilyStr = value;
                NotifyChange(x => x.FontFamilyStr);
                NotifyChange(x => x.FontFamily);
            }
        }

        [JsonIgnore]
        [ApplyToResource]
        public FontFamily FontFamily => new FontFamily(_fontFamilyStr);

        public string LogSave { get; set; }
        public string ModelEncoder { get; set; }
        public string ModelDecoder { get; set; }
        public string ModelJoiner { get; set; }
        public string ModelTokens { get; set; }

        public static Settings Default => new Settings()
        {
            FontColor = Colors.Red,
            StrokeColor = Colors.White,
            HoverBgColor = Color.FromArgb(0x66, 0xA9, 0xCE, 0xFF),
            ListBgColor = Color.FromArgb(0x88, 0xA9, 0xCE, 0xFF),
            FontSize = 32,
            StrokeSize = 1,
            FontFamilyStr = "黑体",
            ModelDecoder = "models\\decoder.onnx",
            ModelEncoder = "models\\encoder.onnx",
            ModelJoiner = "models\\joiner.onnx",
            ModelTokens = "models\\tokens.txt",
            LogSave = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TMSpeechLogs"),
        };

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyChange(Expression<Func<Settings, object>> exp)
        {
            if (exp.Body.NodeType == ExpressionType.MemberAccess)
            {
                var body = (MemberExpression)exp.Body;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(body.Member.Name));
            }
        }
    }


    static class SettingsManager
    {

        private static string GetPath()
        {
            var dataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(dataPath, "TMSpeech");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return Path.Combine(path, "preference.json");
        }

        public static void Write(Settings settings)
        {
            var output = JsonSerializer.Serialize(settings);
            var path = GetPath();
            File.WriteAllText(path, output);
        }

        public static Settings Read()
        {
            var path = GetPath();
            if (!File.Exists(path))
            {
                return Settings.Default;
            }

            var input = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<Settings>(input);
            return settings;
        }

        public static void Apply(Settings settings)
        {
            var app = App.Current as App;

            foreach (var property in typeof(Settings).GetProperties())
            {
                if (property.CustomAttributes.Any(u => u.AttributeType == typeof(ApplyToResource)))
                {
                    var key = property.Name;
                    var value = property.GetValue(settings, null);
                    if (value != null)
                        app.SetResource(key, value);
                }
            }
        }
    }
}
