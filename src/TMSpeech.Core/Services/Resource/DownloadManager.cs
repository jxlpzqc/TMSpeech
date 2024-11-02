using System.IO.Compression;
using System.Text.RegularExpressions;
using Downloader;

namespace TMSpeech.Core.Services.Resource;

public enum DownloadStatus
{
    Idle,
    Pending,
    Downloading,
    Installing,
    Done,
    Paused,
    Failed
}

public class DownloadItem
{
    public Resource Resource { get; set; }
    public DownloadStatus Status { get; set; }
    public bool IsWorking => Status is DownloadStatus.Downloading or DownloadStatus.Installing;

    public bool IsIndeterminate { get; set; }
    public long Finished { get; set; }
    public long Total { get; set; }
    public double Speed { get; set; }

    internal int _step = 0;
    internal DownloadService? _service;
    internal TaskCompletionSource? _downloadTask;
}

public class DownloadManager
{
    private Dictionary<string, DownloadItem> _tasks = new();

    public IReadOnlyDictionary<string, DownloadItem> Tasks => _tasks;

    public DownloadItem? GetItem(Resource resource)
    {
        if (!_tasks.ContainsKey(resource.ID))
        {
            _tasks.Add(resource.ID, new DownloadItem
            {
                Resource = resource,
                Status = DownloadStatus.Idle
            });
        }

        return _tasks[resource.ID];
    }

    public void StartJob(Resource resource)
    {
        Task.Run(() =>
        {
            lock (_tasks)
            {
                while (_tasks.Count(u => u.Value.IsWorking) >= _maxDownloadSize)
                {
                    Monitor.Wait(_tasks);
                }

                DownloadItem task;

                if (!_tasks.ContainsKey(resource.ID))
                {
                    task = new DownloadItem
                    {
                        Resource = resource,
                        Status = DownloadStatus.Pending
                    };
                    _tasks.Add(resource.ID, task);
                    NotifyDownloadStatus(_tasks[resource.ID]);
                    DoJob(task);
                }
                else
                {
                    task = _tasks[resource.ID];
                    if (task.Status != DownloadStatus.Paused || task._service == null) return;
                    task.Status = DownloadStatus.Downloading;
                    NotifyDownloadStatus(task);
                    task._service.Resume();
                }
            }
        });
    }

    public void PauseJob(Resource resource)
    {
        lock (_tasks)
        {
            var task = _tasks[resource.ID];
            if (task.Status != DownloadStatus.Downloading) return;
            task._service.Pause();
            task.Status = DownloadStatus.Paused;
            NotifyDownloadStatus(_tasks[resource.ID]);
            Monitor.Pulse(_tasks);
        }
    }

    private static string GetPluginDirName(Resource res)
    {
        // security check
        var strTheseAreInvalidFileNameChars = new string(System.IO.Path.GetInvalidFileNameChars());
        var regInvalidFileName = new Regex("[" + Regex.Escape(strTheseAreInvalidFileNameChars) + "]");
        if (regInvalidFileName.IsMatch(res.ID))
        {
            throw new Exception("id contains illegal filename");
        }

        var dir = Path.Combine(ConfigManagerFactory.Instance.UserDataDir, res.ID);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return dir;
    }

    private static string GetDownloadingFileName(Resource res, int step)
    {
        var dir = Path.Combine(GetPluginDirName(res), "downloading");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return Path.Combine(dir, step + ".tmp");
    }

    private async Task DoExtract(DownloadItem task)
    {
        var currrentStep = task.Resource.ModuleInfo.InstallSteps[task._step];
        task.IsIndeterminate = true;
        UpdateJobStatus(task, DownloadStatus.Installing);
        await Task.Run(() =>
        {
            var fileStep = currrentStep.ExtractStep ?? task._step - 1;
            ZipFile.ExtractToDirectory(GetDownloadingFileName(task.Resource, fileStep),
                string.IsNullOrEmpty(currrentStep.ExtractTo)
                    ? GetPluginDirName(task.Resource)
                    : Path.Combine(GetPluginDirName(task.Resource), currrentStep.ExtractTo)
            );
        });
    }

    private async Task DoWriteFile(DownloadItem task)
    {
        task.IsIndeterminate = true;
        UpdateJobStatus(task, DownloadStatus.Installing);
        var currrentStep = task.Resource.ModuleInfo.InstallSteps[task._step];
        File.WriteAllText(currrentStep.WritePath, currrentStep.WriteContent);
    }

    private void UpdateJobStatus(DownloadItem task, DownloadStatus newStatus)
    {
        lock (_tasks)
        {
            task.Status = newStatus;
            NotifyDownloadStatus(task);
            Monitor.Pulse(_tasks);
        }
    }

    private async Task DoJob(DownloadItem task)
    {
        var installsteps = task.Resource.ModuleInfo.InstallSteps;
        if (installsteps == null)
        {
            UpdateJobStatus(task, DownloadStatus.Done);
            return;
        }


        for (; task._step < installsteps.Count; task._step++)
        {
            var currentStepIdx = task._step;
            var step = installsteps[currentStepIdx];

            if (step.Type == "download")
            {
                try
                {
                    await DoDownload(task);
                }
                catch
                {
                    UpdateJobStatus(task, DownloadStatus.Failed);
                    return;
                    // TODO: reason
                }
            }
            else if (step.Type == "extract")
            {
                try
                {
                    await DoExtract(task);
                }
                catch
                {
                    UpdateJobStatus(task, DownloadStatus.Failed);
                    return;
                }
            }
            else if (step.Type == "write_file")
            {
                try
                {
                    await DoWriteFile(task);
                }
                catch (Exception e)
                {
                    UpdateJobStatus(task, DownloadStatus.Failed);
                    return;
                }
            }
        }
    }

    private Task DoDownload(DownloadItem task)
    {
        var t = new TaskCompletionSource();

        if (task._service != null)
        {
            task._service.Resume();
            t.SetResult();
            return t.Task;
        }

        task._service = new DownloadService();

        task._service.Pause();
        task._service.CancelAsync();
        task._service.DownloadFileCompleted += (sender, args) =>
        {
            if (args.Cancelled || args.Error != null)
            {
                UpdateJobStatus(task, DownloadStatus.Failed);
            }
            else
            {
                UpdateJobStatus(task, DownloadStatus.Done);
            }

            t.SetResult();
        };
        task._service.DownloadProgressChanged += (sender, args) =>
        {
            task.Finished = args.ReceivedBytesSize;
            task.Total = args.TotalBytesToReceive;
            task.Speed = args.BytesPerSecondSpeed;
            NotifyDownloadStatus(task);
        };


        task._service.DownloadFileTaskAsync(task.Resource.ModuleInfo.InstallSteps[task._step].DownloadURL,
            GetDownloadingFileName(task.Resource, task._step));
        task.Status = DownloadStatus.Downloading;
        NotifyDownloadStatus(task);
        return t.Task;
    }

    private readonly int _maxDownloadSize = 3;

    public event EventHandler<DownloadItem> DownloadStatusUpdated;

    private void NotifyDownloadStatus(DownloadItem item)
    {
        DownloadStatusUpdated.Invoke(this, item);
    }

    internal DownloadManager()
    {
    }
}

public static class DownloadManagerFactory
{
    private static Lazy<DownloadManager> _instance = new(() => new DownloadManager());
    public static DownloadManager Instance => _instance.Value;
}