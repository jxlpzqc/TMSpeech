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
using TMSpeech.Core;
using TMSpeech.Core.Plugins;

namespace TMSpeech.GUI.ViewModels
{
    class ConfigJsonValueAttribute : Attribute
    {
    }

    public abstract class ConfigViewModelBase : ViewModelBase
    {
        protected abstract string SectionName { get; }

        public bool IsModified => ConfigManagerFactory.Instance.IsModified;

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
                    var value = dict[p.Name];
                    if (value != null)
                    {
                        var type = p.PropertyType;
                        p.SetValue(this, value);
                    }
                });
        }

        public void Reset()
        {
            ConfigManagerFactory.Instance.Reset();
            Load();
        }

        public void Load()
        {
            var dict = ConfigManagerFactory.Instance.GetAll();
            Deserialize(dict);
        }

        public void Save()
        {
            try
            {
                ConfigManagerFactory.Instance.Save();
            }
            catch
            {
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
            this.WhenAnyValue(x => x.CurrentTab)
                .Buffer(2, 1);
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