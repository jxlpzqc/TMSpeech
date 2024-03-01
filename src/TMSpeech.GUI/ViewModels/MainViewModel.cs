using System;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using TMSpeech.Core;
using TMSpeech.Core.Plugins;

namespace TMSpeech.GUI.ViewModels;

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
    public int RunningSeconds { get; }

    [ObservableAsProperty]
    public string RunningTimeDisplay { get; }

    [ObservableAsProperty]
    public string Text { get; }

    public ReactiveCommand<Unit, Unit> PlayCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }

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

        this.WhenAnyValue(x => x.Status)
            .Select(x => x == JobStatus.Stopped || x == JobStatus.Paused)
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