using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using TMSpeech.Core.Services.Resource;

namespace TMSpeech.GUI.ViewModels;

public class ResourceItemViewModel : ViewModelBase
{
    public Resource ResouceInfo { get; private set; }

    [ObservableAsProperty]
    public bool IsInstalled { get; }

    [ObservableAsProperty]
    public bool IsInstallButtonShown { get; }

    [ObservableAsProperty]
    public bool IsInstallButtonUpdate { get; }

    [ObservableAsProperty]
    public bool IsPauseButtonShown { get; }

    [ObservableAsProperty]
    public bool IsUninstallButtonShown { get; }

    [ObservableAsProperty]
    public bool IsProgressShown { get; }

    [ObservableAsProperty]
    public int Progress { get; }

    [ObservableAsProperty]
    public bool IsIndeterminate { get; }

    [ObservableAsProperty]
    public string Speed { get; }


    public ReactiveCommand<Unit, Unit> InstallCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    public ReactiveCommand<Unit, Unit> UninstallCommand { get; }

    public ResourceItemViewModel(Resource res)
    {
        ResouceInfo = res;
        InstallCommand = ReactiveCommand.Create(InstallImpl);
        PauseCommand = ReactiveCommand.Create(PauseImpl);
        UninstallCommand = ReactiveCommand.Create(UninstallImpl);

        var downloadItemObservable = Observable.Return(DownloadManagerFactory.Instance.GetItem(res)).Merge(
            Observable.FromEventPattern<DownloadItem>(
                x => DownloadManagerFactory.Instance.DownloadStatusUpdated += x,
                x => DownloadManagerFactory.Instance.DownloadStatusUpdated -= x).Select(
                x => x.EventArgs).Where(x => x.Resource == this.ResouceInfo)
        );

        downloadItemObservable.Select(x => x.Resource.IsLocal)
            .ToPropertyEx(this, x => x.IsInstalled);

        downloadItemObservable.Select(x => !x.IsWorking && (!x.Resource.IsLocal || x.Resource.NeedUpdate))
            .ToPropertyEx(this, x => x.IsInstallButtonShown);

        downloadItemObservable.Select(x => !x.IsWorking && x.Resource.NeedUpdate)
            .ToPropertyEx(this, x => x.IsInstallButtonUpdate);

        downloadItemObservable.Select(x => x.Status == DownloadStatus.Downloading)
            .ToPropertyEx(this, x => x.IsPauseButtonShown);

        downloadItemObservable.Select(x => !x.IsWorking && x.Resource.IsLocal)
            .ToPropertyEx(this, x => x.IsUninstallButtonShown);

        downloadItemObservable.Select(x => x.IsWorking)
            .ToPropertyEx(this, x => x.IsProgressShown);

        downloadItemObservable.Select(x => x.IsIndeterminate)
            .ToPropertyEx(this, x => x.IsIndeterminate);

        downloadItemObservable.Select(x =>
                x.IsIndeterminate ? -1 : x.Finished * 100 / x.Total)
            .ToPropertyEx(this, x => x.Progress);

        downloadItemObservable.Select(x => x.Speed)
            .Select(SpeedToStr)
            .ToPropertyEx(this, x => x.Speed);
    }

    private void InstallImpl()
    {
        DownloadManagerFactory.Instance.DownloadItem(ResouceInfo);
    }

    private void PauseImpl()
    {
        DownloadManagerFactory.Instance.PauseItem(ResouceInfo);
    }

    private void UninstallImpl()
    {
        throw new System.NotImplementedException();
    }

    private string SpeedToStr(double bytesPerSecond)
    {
        return bytesPerSecond switch
        {
            < 1024 => "< 1kb/s",
            < 1024 * 1024 => $"{bytesPerSecond / 1024:2F} KiB/s",
            _ => $"{bytesPerSecond / 1024 / 1024: 2F} MiB/s"
        };
    }
}

public class ResourceManagerViewModel : ViewModelBase
{
    [Reactive]
    public List<ResourceItemViewModel> Items { get; private set; } = new();

    [ObservableAsProperty]
    public bool Loading { get; }

    [ObservableAsProperty]
    public string LoadMessage { get; }

    public ReactiveCommand<Unit, Unit> LoadCommand { get; }

    private async Task Load()
    {
        var local = await ResourceManagerFactory.Instance.GetLocalResources();
        Items = local.Select(u => new ResourceItemViewModel(u)).ToList();
        var remote = await ResourceManagerFactory.Instance.GetRemoteResources();
        Items = local.Union(remote).Select(u => new ResourceItemViewModel(u)).ToList();
    }

    public ResourceManagerViewModel()
    {
        LoadCommand = ReactiveCommand.CreateFromTask(Load);
        LoadCommand.IsExecuting.ToPropertyEx(this, x => x.Loading);
        LoadCommand.ThrownExceptions.Select(u => u.Message)
            .ToPropertyEx(this, x => x.LoadMessage);
    }
}