using System;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Media;
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
        return Observable.Return(ConfigManagerFactory.Instance.Get<T>($"appearance.{key}"))
            .Merge(
                Observable.FromEventPattern<ConfigChangedEventArgs>(
                        p => ConfigManagerFactory.Instance.ConfigChanged += p,
                        p => ConfigManagerFactory.Instance.ConfigChanged -= p)
                    .Where(x => x.EventArgs.Contains($"appearance.{key}"))
                    .Select(x =>
                        ConfigManagerFactory.Instance.Get<T>($"appearance.{key}")
                    ));
    }

    public CaptionStyleViewModel()
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
                AppearanceSectionConfigViewModel.TextAlignEnum.Left => TextAlignment.Left,
                AppearanceSectionConfigViewModel.TextAlignEnum.Center => TextAlignment.Center,
                AppearanceSectionConfigViewModel.TextAlignEnum.Right => TextAlignment.Right,
                AppearanceSectionConfigViewModel.TextAlignEnum.Justify => TextAlignment.Right,
                _ => TextAlignment.Left
            })
            .ToPropertyEx(this, x => x.TextAlign);

        GetPropObservable<string>("FontFamily")
            .Select(x => new FontFamily(x))
            .ToPropertyEx(this, x => x.FontFamily);

        GetPropObservable<uint>("MouseHover")
            .Select(Color.FromUInt32)
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
    public int RunningSeconds { get; }

    [ObservableAsProperty]
    public string RunningTimeDisplay { get; }

    public CaptionStyleViewModel CaptionStyle { get; } = new CaptionStyleViewModel();

    [ObservableAsProperty]
    public string Text { get; }

    public ReactiveCommand<Unit, Unit> PlayCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> HistoryCommand { get; }

    private readonly JobController _jobController;

    public MainViewModel()
    {
        _jobController = JobControllerFactory.GetInstance();
        Observable.FromEventPattern<JobStatus>(
                p => { _jobController.StatusChanged += p; },
                p => { _jobController.StatusChanged -= p; }
            )
            .Select(x => x.EventArgs)
            .Merge(Observable.Return(_jobController.Status))
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

        this.PlayCommand = ReactiveCommand.Create(() => { _jobController.Start(); },
            this.WhenAnyValue(x => x.PlayButtonVisible));
        this.PauseCommand = ReactiveCommand.Create(() => { _jobController.Pause(); },
            this.WhenAnyValue(x => x.PauseButtonVisible));
        this.StopCommand = ReactiveCommand.Create(() => { _jobController.Stop(); },
            this.WhenAnyValue(x => x.StopButtonVisible));
        // TODO
        this.HistoryCommand = ReactiveCommand.Create(() => {  });


        Observable.Interval(TimeSpan.FromSeconds(1))
            .CombineLatest(this.WhenAnyValue(x => x.PlayButtonVisible))
            .Where(x => x.Second == false)
            .Select(x => Unit.Default)
            .Scan(0, (x, _) => x + 1)
            .ToPropertyEx(this, x => x.RunningSeconds);

        this.WhenAnyValue(x => x.RunningSeconds)
            .Select(x => string.Format("{0:D2}:{1:D2}:{2:D2}", x / 60 / 60, (x / 60) % 60, x % 60))
            .ToPropertyEx(this, x => x.RunningTimeDisplay);

        Observable.FromEventPattern<SpeechEventArgs>(
                p => _jobController.TextChanged += p,
                p => _jobController.TextChanged -= p)
            .Select(x => x.EventArgs.Text.Text)
            .Merge(Observable.Return("欢迎使用TMSpeech"))
            .ToPropertyEx(this, x => x.Text);
    }
}