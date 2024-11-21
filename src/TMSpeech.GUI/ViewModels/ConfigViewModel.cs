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
using System.Reflection;
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
        public string Key { get; }

        public ConfigJsonValueAttribute(string key)
        {
            Key = key;
        }

        public ConfigJsonValueAttribute()
        {
        }
    }


    public abstract class SectionConfigViewModelBase : ViewModelBase
    {
        protected virtual string SectionName => "";

        private string PropertyToKey(PropertyInfo prop)
        {
            var key = prop.GetCustomAttributes(typeof(ConfigJsonValueAttribute), false)
                .Select(u => u as ConfigJsonValueAttribute)
                .FirstOrDefault()?.Key;

            if (key != null) return key;
            return $"{SectionName}.{prop.Name}";
        }

        public virtual Dictionary<string, object> Serialize()
        {
            var ret = new Dictionary<string, object>();
            this.GetType().GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(ConfigJsonValueAttribute), false).Length > 0)
                .ToList()
                .ForEach(p =>
                {
                    var value = p.GetValue(this);
                    ret[PropertyToKey(p)] = value;
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
                    if (!dict.ContainsKey(PropertyToKey(p))) return;
                    var value = dict[PropertyToKey(p)];
                    var type = p.PropertyType;
                    p.SetValue(this, Convert.ChangeType(value, type));
                });
        }

        public void Load()
        {
            var dict = ConfigManagerFactory.Instance.GetAll();
            Deserialize(
                dict.Where(x => ConfigManager.IsInSection(x.Key, SectionName))
                    .ToDictionary(x => x.Key, x => x.Value)
            );
        }

        public void Apply()
        {
            var dict = Serialize();
            ConfigManagerFactory.Instance.BatchApply(dict.Where(u => u.Value != null)
                .ToDictionary(x => x.Key, x => x.Value));
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
        public NotificationConfigViewModel NotificationConfig { get; } = new NotificationConfigViewModel();

        [ObservableAsProperty]
        public bool IsNotRunning { get; }

        [Reactive]
        public int CurrentTab { get; set; } = 1;

        public ConfigViewModel()
        {
            Observable.Return(JobManagerFactory.Instance.Status != JobStatus.Running).Merge(
                Observable.FromEventPattern<JobStatus>(
                    x => JobManagerFactory.Instance.StatusChanged += x,
                    x => JobManagerFactory.Instance.StatusChanged -= x
                ).Select(x => x.EventArgs != JobStatus.Running)
            ).ToPropertyEx(this, x => x.IsNotRunning);
        }
    }

    public class GeneralSectionConfigViewModel : SectionConfigViewModelBase
    {
        protected override string SectionName => GeneralConfigTypes.SectionName;

        [Reactive]
        [ConfigJsonValue]
        public string Language { get; set; }

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
        public string ResultLogPath { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public bool LaunchOnStartup { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public bool StartOnLaunch { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public bool AutoUpdate { get; set; }

        // Left, Top, Width, Height
        [Reactive]
        [ConfigJsonValue]
        public List<int> MainWindowLocation { get; set; } = [];
    }

    public class AppearanceSectionConfigViewModel : SectionConfigViewModelBase
    {
        protected override string SectionName => AppearanceConfigTypes.SectionName;

        public List<FontFamily> FontsAvailable { get; private set; }

        [Reactive]
        [ConfigJsonValue]
        public uint ShadowColor { get; set; }


        [Reactive]
        [ConfigJsonValue]
        public int ShadowSize { get; set; }


        [Reactive]
        [ConfigJsonValue]
        public string FontFamily { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public int FontSize { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public uint FontColor { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public uint MouseHover { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public int TextAlign { get; set; }

        [Reactive]
        [ConfigJsonValue(AppearanceConfigTypes.BackgroundColor)]
        public uint BackgroundColor { get; set; }

        public List<KeyValuePair<int, string>> TextAligns { get; } =
        [
            new KeyValuePair<int, string>(AppearanceConfigTypes.TextAlignEnum.Left, "左对齐"),
            new KeyValuePair<int, string>(AppearanceConfigTypes.TextAlignEnum.Center, "居中对齐"),
            new KeyValuePair<int, string>(AppearanceConfigTypes.TextAlignEnum.Right, "右对齐"),
            new KeyValuePair<int, string>(AppearanceConfigTypes.TextAlignEnum.Justify, "两端对齐"),
        ];

        public AppearanceSectionConfigViewModel()
        {
            FontsAvailable = FontManager.Current.SystemFonts.ToList();
        }
    }

    public class NotificationConfigViewModel : SectionConfigViewModelBase
    {
        protected override string SectionName => NotificationConfigTypes.SectionName;


        public List<KeyValuePair<int, string>> NotificaitonTypes { get; } =
        [
            new KeyValuePair<int, string>(NotificationConfigTypes.NotificationTypeEnum.None, "关闭通知"),
            new KeyValuePair<int, string>(NotificationConfigTypes.NotificationTypeEnum.System, "系统通知 (暂不支持 macOS)"),
            // new KeyValuePair<int, string>(NotificationTypeEnum.Custom, "TMSpeech 通知"),
        ];

        [Reactive]
        [ConfigJsonValue]
        public int NotificationType { get; set; } = NotificationConfigTypes.NotificationTypeEnum.System;

        [Reactive]
        [ConfigJsonValue]
        public string SensitiveWords { get; set; } = "";
    }

    public class AudioSectionConfigViewModel : SectionConfigViewModelBase
    {
        [Reactive]
        [ConfigJsonValue]
        public string AudioSource { get; set; }

        [ObservableAsProperty]
        public IReadOnlyDictionary<string, Core.Plugins.IAudioSource> AudioSourcesAvailable { get; }

        [ObservableAsProperty]
        public IPluginConfigEditor? ConfigEditor { get; }

        [Reactive]
        [ConfigJsonValue]
        public string PluginConfig { get; set; } = "";

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public IReadOnlyDictionary<string, Core.Plugins.IAudioSource> Refresh()
        {
            var plugins = Core.Plugins.PluginManagerFactory.GetInstance().AudioSources;
            if (AudioSource == "" && plugins.Count >= 1)
                AudioSource = plugins.First().Key;
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
            if (dict.ContainsKey(AudioSourceConfigTypes.AudioSource))
            {
                AudioSource = dict[AudioSourceConfigTypes.AudioSource]?.ToString() ?? "";
            }

            if (dict.ContainsKey(AudioSourceConfigTypes.GetPluginConfigKey(AudioSource)))
            {
                PluginConfig = dict[AudioSourceConfigTypes.GetPluginConfigKey(AudioSource)]?.ToString() ?? "";
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
                .Select(x => AudioSourcesAvailable.FirstOrDefault(u => u.Key == x))
                .Select(x =>
                {
                    var plugin = x.Value;
                    var editor = plugin?.CreateConfigEditor();
                    var config = ConfigManagerFactory.Instance.Get<string>(
                        AudioSourceConfigTypes.GetPluginConfigKey(AudioSource));
                    editor?.LoadConfigString(config);
                    return editor;
                })
                .ToPropertyEx(this, x => x.ConfigEditor);


            this.WhenAnyValue(x => x.ConfigEditor)
                .Subscribe(x =>
                {
                    var config =
                        ConfigManagerFactory.Instance.Get<string>(
                            AudioSourceConfigTypes.GetPluginConfigKey(AudioSource));
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
        public IReadOnlyDictionary<string, Core.Plugins.IRecognizer> RecognizersAvailable { get; }

        [ObservableAsProperty]
        public IPluginConfigEditor? ConfigEditor { get; }

        [Reactive]
        [ConfigJsonValue]
        public string PluginConfig { get; set; } = "";

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public IReadOnlyDictionary<string, Core.Plugins.IRecognizer> Refresh()
        {
            var plugins = Core.Plugins.PluginManagerFactory.GetInstance().Recognizers;
            if (Recognizer == "" && plugins.Count >= 1)
                Recognizer = plugins.First().Key;
            return plugins;
        }

        public override Dictionary<string, object> Serialize()
        {
            var ret = new Dictionary<string, object>
            {
                { RecognizerConfigTypes.Recognizer, Recognizer },
            };
            if (!string.IsNullOrEmpty(Recognizer))
            {
                ret.Add(RecognizerConfigTypes.GetPluginConfigKey(Recognizer), PluginConfig);
            }

            return ret;
        }

        public override void Deserialize(IReadOnlyDictionary<string, object> dict)
        {
            if (dict.ContainsKey(RecognizerConfigTypes.Recognizer))
            {
                Recognizer = dict[RecognizerConfigTypes.Recognizer]?.ToString() ?? "";
            }

            if (dict.ContainsKey(RecognizerConfigTypes.GetPluginConfigKey(Recognizer)))
            {
                PluginConfig = dict[RecognizerConfigTypes.GetPluginConfigKey(Recognizer)]?.ToString() ?? "";
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
                .Select(x => RecognizersAvailable.FirstOrDefault(u => u.Key == x))
                .Select(x =>
                {
                    var plugin = x.Value;
                    var editor = plugin?.CreateConfigEditor();
                    var config = ConfigManagerFactory.Instance.Get<string>(
                        RecognizerConfigTypes.GetPluginConfigKey(Recognizer));
                    editor?.LoadConfigString(config);
                    return editor;
                })
                .ToPropertyEx(this, x => x.ConfigEditor);

            this.WhenAnyValue(x => x.ConfigEditor)
                .Subscribe(x =>
                {
                    var config = ConfigManagerFactory.Instance.Get<string>(
                        RecognizerConfigTypes.GetPluginConfigKey(Recognizer));
                    PluginConfig = config;
                });
        }
    }
}