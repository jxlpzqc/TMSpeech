using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Media;
using ReactiveUI;
using TMSpeech.Core;
using TMSpeech.Core.Plugins;

namespace TMSpeech.GUI.ViewModels
{
    class ConfigJsonValueAttribute : Attribute
    {
    }

    public abstract class SectionConfigViewModelBase : ViewModelBase
    {
        protected virtual string SectionName => "";

        public virtual Dictionary<string, object> Serialize()
        {
            var ret = new Dictionary<string, object>();
            this.GetType().GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(ConfigJsonValueAttribute), false).Length > 0)
                .ToList()
                .ForEach(p =>
                {
                    var value = p.GetValue(this);
                    ret[p.Name] = value;
                });
            return ret;
        }

        public virtual void Deserialize(IReadOnlyDictionary<string, object> dict)
        {
            this.GetType().GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(ConfigJsonValueAttribute), false).Length > 0)
                .ToList()
                .ForEach(p =>
                {
                    if (!dict.ContainsKey(p.Name)) return;
                    var value = dict[p.Name];
                    var type = p.PropertyType;
                    p.SetValue(this, Convert.ChangeType(value, type));
                });
        }

        public void Load()
        {
            var dict = ConfigManagerFactory.Instance.GetAll();
            Deserialize(
                dict.Where(x => ConfigManager.IsInSection(x.Key, SectionName))
                    .ToDictionary(
                        x => string.IsNullOrEmpty(SectionName) ? x.Key : x.Key.Substring(SectionName.Length + 1),
                        x => x.Value
                    )
            );
        }

        public void Apply()
        {
            var dict = Serialize();
            ConfigManagerFactory.Instance.BatchApply(dict.Where(u => u.Value != null).ToDictionary(
                x => (SectionName != "" ? $"{SectionName}." : "") + x.Key,
                x => x.Value
            ));

        }

        public SectionConfigViewModelBase()
        {
            Load();
            this.PropertyChanged += (sender, args) =>
            {
                var propName = args.PropertyName;
                var type = sender.GetType();

                if (sender.GetType().GetProperty(propName)
                    .GetCustomAttributes(false)
                    .Any(u => u.GetType() == typeof(ConfigJsonValueAttribute)))
                {
                    Apply();
                }
            };
        }
    }

    public class ConfigViewModel : ViewModelBase
    {
        public GeneralSectionConfigViewModel GeneralSectionConfig { get; } = new GeneralSectionConfigViewModel();

        public AppearanceSectionConfigViewModel AppearanceSectionConfig { get; } =
            new AppearanceSectionConfigViewModel();

        public AudioSectionConfigViewModel AudioSectionConfig { get; } = new AudioSectionConfigViewModel();
        public RecognizeSectionConfigViewModel RecognizeSectionConfig { get; } = new RecognizeSectionConfigViewModel();

        [Reactive]
        public int CurrentTab { get; set; } = 0;
    }

    public class GeneralSectionConfigViewModel : SectionConfigViewModelBase
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

        //[Reactive]
        //[ConfigJsonValue]
        //public string UserDir { get; set; } = "D:\\TMSpeech";

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

    public class AppearanceSectionConfigViewModel : SectionConfigViewModelBase
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

        public AppearanceSectionConfigViewModel()
        {
            FontsAvailable = FontManager.Current.SystemFonts.ToList();
        }
    }

    public class AudioSectionConfigViewModel : SectionConfigViewModelBase
    {
        [Reactive]
        [ConfigJsonValue]
        public string AudioSource { get; set; } = "";

        [ObservableAsProperty]
        public IReadOnlyList<Core.Plugins.IAudioSource> AudioSourcesAvailable { get; } =
            new List<Core.Plugins.IAudioSource>();

        [ObservableAsProperty]
        public IPluginConfigEditor? ConfigEditor { get; }

        [Reactive]
        [ConfigJsonValue]
        public string PluginConfig { get; set; } = "";

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public IReadOnlyList<Core.Plugins.IAudioSource> Refresh()
        {
            var plugins = Core.Plugins.PluginManagerFactory.GetInstance().AudioSources;
            if (AudioSource == "" && plugins.Count >= 1)
                AudioSource = plugins[0].Name;
            return plugins;
        }

        public override Dictionary<string, object> Serialize()
        {
            var ret = new Dictionary<string, object>
            {
                { "audio.source", AudioSource },
            };
            if (!string.IsNullOrEmpty(AudioSource))
            {
                ret.Add($"plugin.{AudioSource}.config", PluginConfig);
            }

            return ret;
        }

        public override void Deserialize(IReadOnlyDictionary<string, object> dict)
        {
            if (dict.ContainsKey("audio.source"))
            {
                AudioSource = dict["audio.source"]?.ToString() ?? "";
            }

            if (dict.ContainsKey($"plugin.{AudioSource}.config"))
            {
                PluginConfig = dict[$"plugin.{AudioSource}.config"]?.ToString() ?? "";
            }
        }

        public AudioSectionConfigViewModel()
        {
            this.RefreshCommand = ReactiveCommand.Create(() => { });
            this.RefreshCommand.Merge(Observable.Return(Unit.Default))
                .SelectMany(u => Observable.FromAsync(() => Task.Run(() => Refresh())))
                .ToPropertyEx(this, x => x.AudioSourcesAvailable);

            this.WhenAnyValue(u => u.AudioSource, u => u.AudioSourcesAvailable)
                .Where((u) => u.Item1 != null && u.Item2 != null)
                .Select(u => u.Item1)
                .Where(x => !string.IsNullOrEmpty(x))
                .DistinctUntilChanged()
                .Select(x => AudioSourcesAvailable.FirstOrDefault(u => u.Name == x))
                .Select(x =>
                {
                    var editor = x?.CreateConfigEditor();
                    var config = ConfigManagerFactory.Instance.Get<string>($"plugin.{AudioSource}.config");
                    editor?.LoadConfigString(config);
                    return x?.CreateConfigEditor();
                })
                .ToPropertyEx(this, x => x.ConfigEditor);


            this.WhenAnyValue(x => x.ConfigEditor)
                .Subscribe(x =>
                {
                    var config = ConfigManagerFactory.Instance.Get<string>($"plugin.{AudioSource}.config");
                    PluginConfig = config;
                });
        }
    }


    public class RecognizeSectionConfigViewModel : SectionConfigViewModelBase
    {
        protected override string SectionName => "";

        [Reactive]
        [ConfigJsonValue]
        public string Recognizer { get; set; } = "";

        [ObservableAsProperty]
        public IReadOnlyList<Core.Plugins.IRecognizer> RecognizersAvailable { get; } =
            new List<Core.Plugins.IRecognizer>();

        [ObservableAsProperty]
        public IPluginConfigEditor? ConfigEditor { get; }

        [Reactive]
        [ConfigJsonValue]
        public string PluginConfig { get; set; } = "";

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public IReadOnlyList<Core.Plugins.IRecognizer> Refresh()
        {
            var plugins = Core.Plugins.PluginManagerFactory.GetInstance().Recognizers;
            if (Recognizer == "" && plugins.Count >= 1)
                Recognizer = plugins[0].Name;
            return plugins;
        }

        public override Dictionary<string, object> Serialize()
        {
            var ret = new Dictionary<string, object>
            {
                { "recognizer.source", Recognizer },
            };
            if (!string.IsNullOrEmpty(Recognizer))
            {
                ret.Add($"plugin.{Recognizer}.config", PluginConfig);
            }

            return ret;
        }

        public override void Deserialize(IReadOnlyDictionary<string, object> dict)
        {
            if (dict.ContainsKey("recognizer.source"))
            {
                Recognizer = dict["recognizer.source"]?.ToString() ?? "";
            }

            if (dict.ContainsKey($"plugin.{Recognizer}.config"))
            {
                PluginConfig = dict[$"plugin.{Recognizer}.config"]?.ToString() ?? "";
            }
        }

        public RecognizeSectionConfigViewModel()
        {
            this.RefreshCommand = ReactiveCommand.Create(() => { });
            this.RefreshCommand.Merge(Observable.Return(Unit.Default))
                .SelectMany(u => Observable.FromAsync(() => Task.Run(() => Refresh())))
                .ToPropertyEx(this, x => x.RecognizersAvailable);

            this.WhenAnyValue(u => u.Recognizer, u => u.RecognizersAvailable)
                .Where((u) => u.Item1 != null && u.Item2 != null)
                .Select(u => u.Item1)
                .Where(x => !string.IsNullOrEmpty(x))
                .DistinctUntilChanged()
                .Select(x => RecognizersAvailable.FirstOrDefault(u => u.Name == x))
                .Select(x => x?.CreateConfigEditor())
                .ToPropertyEx(this, x => x.ConfigEditor);

            this.WhenAnyValue(x => x.ConfigEditor)
                .Subscribe(x =>
                {
                    var config = ConfigManagerFactory.Instance.Get<string>($"plugin.{Recognizer}.config");
                    PluginConfig = config;
                });
        }
    }
}