using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Media;
using ReactiveUI;
using TMSpeech.Core.Plugins;

namespace TMSpeech.GUI.ViewModels
{
    class ConfigJsonValueAttribute : Attribute
    {
    }

    public abstract class ConfigViewModelBase : ViewModelBase
    {
        protected abstract string SectionName { get; }

        public bool IsModified
        {
            get { return _backupFile != Serialize().ToJsonString(); }
        }

        public virtual JsonObject Serialize()
        {
            var json = new JsonObject();
            this.GetType().GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(ConfigJsonValueAttribute), false).Length > 0)
                .ToList()
                .ForEach(p =>
                {
                    var value = p.GetValue(this);
                    var type = p.PropertyType;
                    if (value != null)
                    {
                        if (type == typeof(int)) json[p.Name] = (int)value;
                        else if (type == typeof(string)) json[p.Name] = (string)value;
                        else if (type == typeof(bool)) json[p.Name] = (bool)value;
                        else if (type == typeof(double)) json[p.Name] = (double)value;
                        else if (type == typeof(float)) json[p.Name] = (float)value;
                        else if (type == typeof(long)) json[p.Name] = (long)value;
                        else throw new InvalidCastException("Unsupported type");
                    }
                });
            return json;
        }

        public virtual void Deserialize(JsonObject json)
        {
            this.GetType().GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(ConfigJsonValueAttribute), false).Length > 0)
                .ToList()
                .ForEach(p =>
                {
                    var value = json[p.Name];
                    if (value != null)
                    {
                        var type = p.PropertyType;
                        p.SetValue(this, value.Deserialize(type));
                    }
                });
        }

        private string GetConfigFile()
        {
            return "D:\\TMSpeech.config.json";
        }

        private string _backupFile;

        public void Reset()
        {
            Deserialize(JsonNode.Parse(_backupFile) as JsonObject);
        }

        public void Load()
        {
            try
            {
                var filename = GetConfigFile();
                var jsonText = File.ReadAllText(filename);
                var jsonObj = JsonNode.Parse(jsonText) as JsonObject;
                if (!string.IsNullOrEmpty(SectionName))
                {
                    foreach (var section in SectionName.Split(":"))
                    {
                        jsonObj = jsonObj[section] as JsonObject;
                    }
                }

                _backupFile = jsonObj.ToJsonString();
                Deserialize(jsonObj);
            }
            catch
            {
                //TODO: handle error
            }
        }

        public void Save()
        {
            try
            {
                var filename = GetConfigFile();
                var saveJsonObj = Serialize();

                var jsonText = File.ReadAllText(filename);
                var rootJsonObj = JsonNode.Parse(jsonText) as JsonObject;
                var jsonObj = rootJsonObj;
                if (!string.IsNullOrEmpty(SectionName))
                {
                    var sections = SectionName.Split(":");
                    foreach (var section in sections[..-1])
                    {
                        var v = jsonObj[section] as JsonObject;
                        if (v == null)
                        {
                            v = new JsonObject();
                            jsonObj[section] = v;
                        }

                        jsonObj = v;
                    }

                    jsonObj[sections[sections.Length - 1]] = saveJsonObj;
                    File.WriteAllText(filename, rootJsonObj.ToJsonString());
                }
            }
            catch
            {
                //TODO: handle error
            }
        }
    }

    public class ConfigViewModel
    {
        public GeneralConfigViewModel GeneralConfig { get; } = new GeneralConfigViewModel();
        public AppearanceConfigViewModel AppearanceConfig { get; } = new AppearanceConfigViewModel();
        public AudioConfigViewModel AudioConfig { get; } = new AudioConfigViewModel();
        public RecognizeConfigViewModel RecognizeConfig { get; } = new RecognizeConfigViewModel();


        [Reactive]
        public int CurrentTab { get; set; } = 0;

        private const int TAB_GENERAL = 0;
        private const int TAB_APPEARANCE = 1;
        private const int TAB_AUDIO = 2;
        private const int TAB_RECOGNIZE = 3;
        private const int TAB_ABOUT = 4;

        public ConfigViewModel()
        {
        }
    }

    public class GeneralConfigViewModel : ConfigViewModelBase
    {
        protected override string SectionName => "general";

        [Reactive]
        [ConfigJsonValue]
        public string Language { get; set; } = "zh-cn";

        public ObservableCollection<KeyValuePair<string, string>> LanguagesAvailable { get; } =
        [
            new KeyValuePair<string, string>("zh-cn", "简体中文"),
            new KeyValuePair<string, string>("en-us", "English"),
        ];

        /*[Reactive]
        public string Theme { get; set; } = "light";*/

        [Reactive]
        [ConfigJsonValue]
        public string UserDir { get; set; } = "D:\\TMSpeech";

        [Reactive]
        [ConfigJsonValue]
        public bool LaunchOnStartup { get; set; } = false;

        [Reactive]
        [ConfigJsonValue]
        public bool StartOnLaunch { get; set; } = false;

        [Reactive]
        [ConfigJsonValue]
        public bool AutoUpdate { get; set; } = true;
    }

    public class AppearanceConfigViewModel : ConfigViewModelBase
    {
        protected override string SectionName => "appearance";

        public List<FontFamily> FontsAvailable { get; private set; }

        [Reactive]
        [ConfigJsonValue]
        public uint ShadowColor { get; set; } = 0xFF000000;


        [Reactive]
        [ConfigJsonValue]
        public int ShadowSize { get; set; } = 10;


        [Reactive]
        [ConfigJsonValue]
        public string FontFamily { get; set; } = "Arial";

        [Reactive]
        [ConfigJsonValue]
        public int FontSize { get; set; } = 24;

        [Reactive]
        [ConfigJsonValue]
        public uint FontColor { get; set; } = 0xFFFF0000;

        [Reactive]
        [ConfigJsonValue]
        public uint MouseHover { get; set; } = 0xFFFF0000;

        [Reactive]
        [ConfigJsonValue]
        public int TextAlign { get; set; } = TextAlignEnum.Left;

        public static class TextAlignEnum
        {
            public const int Left = 0;
            public const int Center = 1;
            public const int Right = 2;
            public const int Justify = 3;
        }

        public List<KeyValuePair<int, string>> TextAligns { get; } =
        [
            new KeyValuePair<int, string>(TextAlignEnum.Left, "左对齐"),
            new KeyValuePair<int, string>(TextAlignEnum.Center, "居中对齐"),
            new KeyValuePair<int, string>(TextAlignEnum.Right, "右对齐"),
            new KeyValuePair<int, string>(TextAlignEnum.Justify, "两端对齐"),
        ];

        public AppearanceConfigViewModel()
        {
            FontsAvailable = FontManager.Current.SystemFonts.ToList();
        }
    }

    public class AudioConfigViewModel : ConfigViewModelBase
    {
        protected override string SectionName => "audio";

        [Reactive]
        [ConfigJsonValue]
        public string AudioSource { get; set; } = "";

        [ObservableAsProperty]
        public IReadOnlyList<Core.Plugins.IAudioSource> AudioSourcesAvailable { get; } =
            new List<Core.Plugins.IAudioSource>();

        [ObservableAsProperty]
        public IPluginConfiguration? Config { get; }

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public IReadOnlyList<Core.Plugins.IAudioSource> Refresh()
        {
            var plugins = Core.Plugins.PluginManagerFactory.GetInstance().AudioSources;
            if (AudioSource == "" && plugins.Count >= 1)
                AudioSource = plugins[0].Name;
            return plugins;
        }

        public AudioConfigViewModel()
        {
            this.RefreshCommand = ReactiveCommand.Create(() => { });
            this.RefreshCommand.Merge(Observable.Return(Unit.Default))
                .SelectMany(u => Observable.FromAsync(() => Task.Run(() => Refresh())))
                .ToPropertyEx(this, x => x.AudioSourcesAvailable);

            this.WhenAnyValue(u => u.AudioSource, u => u.AudioSourcesAvailable)
                .Where((u) => u.Item1 != null && u.Item2 != null)
                .Select(u => u.Item1)
                .Select(x => AudioSourcesAvailable.FirstOrDefault(u => u.Name == x))
                .Select(x => x?.Configuration)
                .ToPropertyEx(this, x => x.Config);
        }
    }


    public class RecognizeConfigViewModel : ConfigViewModelBase
    {
        protected override string SectionName => "recognize";

        [Reactive]
        [ConfigJsonValue]
        public string Recognizer { get; set; } = "";

        [ObservableAsProperty]
        public IReadOnlyList<Core.Plugins.IRecognizer> RecognizersAvailable { get; } =
            new List<Core.Plugins.IRecognizer>();

        [ObservableAsProperty]
        public IPluginConfiguration? Config { get; }

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public IReadOnlyList<Core.Plugins.IRecognizer> Refresh()
        {
            var plugins = Core.Plugins.PluginManagerFactory.GetInstance().Recognizers;
            if (Recognizer == "" && plugins.Count >= 1)
                Recognizer = plugins[0].Name;
            return plugins;
        }

        public RecognizeConfigViewModel()
        {
            this.RefreshCommand = ReactiveCommand.Create(() => { });
            this.RefreshCommand.Merge(Observable.Return(Unit.Default))
                .SelectMany(u => Observable.FromAsync(() => Task.Run(() => Refresh())))
                .ToPropertyEx(this, x => x.RecognizersAvailable);

            this.WhenAnyValue(u => u.Recognizer, u => u.RecognizersAvailable)
                .Where((u) => u.Item1 != null && u.Item2 != null)
                .Select(u => u.Item1)
                .Select(x => RecognizersAvailable.FirstOrDefault(u => u.Name == x))
                .Select(x => x?.Configuration)
                .ToPropertyEx(this, x => x.Config);
        }
    }
}