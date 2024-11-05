using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using TMSpeech.Core;
using TMSpeech.Core.Plugins;

namespace TMSpeech.GUI.ViewModels;

public class CaptionStyleViewModel : ViewModelBase
{
    [ObservableAsProperty]
    public int ShadowSize { get; }

    [ObservableAsProperty]
    public Color ShadowColor { get; }

    [ObservableAsProperty]
    public int FontSize { get; }

    [ObservableAsProperty]
    public Color FontColor { get; }

    [ObservableAsProperty]
    public TextAlignment TextAlign { get; }

    [ObservableAsProperty]
    public FontFamily FontFamily { get; }

    [ObservableAsProperty]
    public Color MouseHover { get; }

    [ObservableAsProperty]
    public string Text { get; }

    private IObservable<T> GetPropObservable<T>(string key)
    {
        return Observable.Return(ConfigManagerFactory.Instance.Get<T>($"{AppearanceConfigTypes.SectionName}.{key}"))
            .Merge(
                Observable.FromEventPattern<ConfigChangedEventArgs>(
                        p => ConfigManagerFactory.Instance.ConfigChanged += p,
                        p => ConfigManagerFactory.Instance.ConfigChanged -= p)
                    .Where(x => x.EventArgs.Contains($"appearance.{key}"))
                    .Select(x =>
                        ConfigManagerFactory.Instance.Get<T>($"{AppearanceConfigTypes.SectionName}.{key}")
                    ));
    }

    public CaptionStyleViewModel(MainViewModel mainViewModel)
    {
        GetPropObservable<int>("ShadowSize")
            .ToPropertyEx(this, x => x.ShadowSize);
        GetPropObservable<uint>("ShadowColor")
            .Select(Color.FromUInt32)
            .ToPropertyEx(this, x => x.ShadowColor);
        GetPropObservable<int>("FontSize")
            .Select(x => { return x; })
            .ToPropertyEx(this, x => x.FontSize);
        GetPropObservable<uint>("FontColor")
            .Select(Color.FromUInt32)
            .ToPropertyEx(this, x => x.FontColor);
        GetPropObservable<int>("TextAlign")
            .Select(x => x switch
            {
                AppearanceConfigTypes.TextAlignEnum.Left => TextAlignment.Left,
                AppearanceConfigTypes.TextAlignEnum.Center => TextAlignment.Center,
                AppearanceConfigTypes.TextAlignEnum.Right => TextAlignment.Right,
                AppearanceConfigTypes.TextAlignEnum.Justify => TextAlignment.Right,
                _ => TextAlignment.Left
            })
            .ToPropertyEx(this, x => x.TextAlign);

        GetPropObservable<string>("FontFamily")
            .Select(x => new FontFamily(x))
            .ToPropertyEx(this, x => x.FontFamily);

        GetPropObservable<uint>("MouseHover")
            .Select(Color.FromUInt32)
            .CombineLatest(mainViewModel.WhenAnyValue(x => x.IsLocked),
                (color, locked) => locked ? Colors.Transparent : color)
            .ToPropertyEx(this, x => x.MouseHover);
    }
}

public class MainViewModel : ViewModelBase
{
    [ObservableAsProperty]
    public JobStatus Status { get; }

    [ObservableAsProperty]
    public bool PlayButtonVisible { get; }

    [ObservableAsProperty]
    public bool PauseButtonVisible { get; }

    [ObservableAsProperty]
    public bool StopButtonVisible { get; }

    [ObservableAsProperty]
    public bool HistroyPanelVisible { get; }

    [ObservableAsProperty]
    public long RunningSeconds { get; }

    [ObservableAsProperty]
    public string RunningTimeDisplay { get; }

    public CaptionStyleViewModel CaptionStyle { get; }

    [ObservableAsProperty]
    public string Text { get; }

    [Reactive]
    public bool IsLocked { get; set; }

    public ObservableCollection<string> HistoryTexts { get; } = new();

    public ReactiveCommand<Unit, Unit> PlayCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> LockCommand { get; }


    public ReactiveCommand<Unit, Unit> HistoryCommand { get; }

    private readonly JobManager _jobManager;

    public MainViewModel()
    {
        _jobManager = JobManagerFactory.Instance;
        CaptionStyle = new CaptionStyleViewModel(this);

        Observable.FromEventPattern<JobStatus>(
                p => { _jobManager.StatusChanged += p; },
                p => { _jobManager.StatusChanged -= p; }
            )
            .Select(x => x.EventArgs)
            .Merge(Observable.Return(_jobManager.Status))
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToPropertyEx(this, x => x.Status);

        this.WhenAnyValue(x => x.Status) // IObservable<JobStatus>
            .Select(x => x == JobStatus.Stopped || x == JobStatus.Paused) // IObservable<bool>
            .ToPropertyEx(this, x => x.PlayButtonVisible);

        this.WhenAnyValue(x => x.Status)
            .Select(x => x == JobStatus.Running)
            .ToPropertyEx(this, x => x.PauseButtonVisible);

        this.WhenAnyValue(x => x.Status)
            .Select(x => x == JobStatus.Running || x == JobStatus.Paused)
            .ToPropertyEx(this, x => x.StopButtonVisible);

        this.LockCommand = ReactiveCommand.Create(() => { IsLocked = true; });

        this.PlayCommand = ReactiveCommand.CreateFromTask(
            async () => { await Task.Run(() => { _jobManager.Start(); }); },
            this.WhenAnyValue(x => x.PlayButtonVisible));
        this.PauseCommand = ReactiveCommand.CreateFromTask(
            async () => { await Task.Run(() => { _jobManager.Pause(); }); },
            this.WhenAnyValue(x => x.PauseButtonVisible));
        this.StopCommand = ReactiveCommand.CreateFromTask(
            async () => { await Task.Run(() => { _jobManager.Stop(); }); },
            this.WhenAnyValue(x => x.StopButtonVisible));

        Observable.FromEventPattern<long>(x => _jobManager.RunningSecondsChanged += x,
                x => _jobManager.RunningSecondsChanged -= x)
            .Select(x => x.EventArgs)
            .ToPropertyEx(this, x => x.RunningSeconds);

        this.WhenAnyValue(x => x.RunningSeconds)
            .Select(x => string.Format("{0:D2}:{1:D2}:{2:D2}", x / 60 / 60, (x / 60) % 60, x % 60))
            .ToPropertyEx(this, x => x.RunningTimeDisplay);

        Observable.FromEventPattern<SpeechEventArgs>(
                p => _jobManager.TextChanged += p,
                p => _jobManager.TextChanged -= p)
            .Select(x => x.EventArgs.Text.Text)
            .Merge(this.PlayCommand.ThrownExceptions.Select(e => $"启动失败！{e.Message}"))
            .Merge(this.PauseCommand.ThrownExceptions.Select(e => $"暂停失败！{e.Message}"))
            .Merge(this.StopCommand.ThrownExceptions.Select(e => $"停止失败！{e.Message}"))
            .Merge(Observable.Return("欢迎使用TMSpeech"))
            .ToPropertyEx(this, x => x.Text);

        Observable.FromEventPattern<SpeechEventArgs>(
                p => _jobManager.SentenceDone += p,
                p => _jobManager.SentenceDone -= p)
            .Subscribe(x => HistoryTexts.Insert(0, DateTime.Now.ToString("[HH:mm:ss] ") + x.EventArgs.Text.Text));
    }
}