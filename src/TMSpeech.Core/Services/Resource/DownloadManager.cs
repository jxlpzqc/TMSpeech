using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Downloader;

namespace TMSpeech.Core.Services.Resource;

public enum DownloadStatus
{
    Pending,
    Downloading,
    Installing,
    Done,
    Pausing,
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

    internal DownloadService? _service;
}

public class DownloadManager
{
    private Dictionary<string, DownloadItem> _tasks = new();

    public IReadOnlyDictionary<string, DownloadItem> Tasks => _tasks;

    public DownloadItem GetItem(Resource resource)
    {
        return _tasks[resource.ID];
    }

    public void DownloadItem(Resource resource)
    {
        lock (_tasks)
        {
            var task = new DownloadItem()
            {
                Resource = resource,
                Status = DownloadStatus.Pending
            };
            _tasks.Add(resource.ID, task);

            NotifyDownloadStatus(_tasks[resource.ID]);

            while (_tasks.Count(u => u.Value.IsWorking) >= _maxDownloadSize)
            {
                Monitor.Wait(_tasks);
            }

            DoDownload(task);
        }
    }

    public void PauseItem(Resource resource)
    {
        lock (_tasks)
        {
            var task = _tasks[resource.ID];
            task._service.Pause();
            task.Status = DownloadStatus.Paused;
            NotifyDownloadStatus(_tasks[resource.ID]);
            Monitor.Pulse(_tasks);
        }
    }

    private static string GetDownloadingFileName(Resource res)
    {
        var dir = Path.Combine(ConfigManagerFactory.Instance.UserDataDir, "downloading");
        if (Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var strTheseAreInvalidFileNameChars = new string(System.IO.Path.GetInvalidFileNameChars());
        var regInvalidFileName = new Regex("[" + Regex.Escape(strTheseAreInvalidFileNameChars) + "]");
        // security check
        if (regInvalidFileName.IsMatch(res.ID))
        {
            throw new Exception("id contains illegal filename");
        }

        return Path.Combine(dir, res.ID + ".tmp");
    }

    private void DoInstall(DownloadItem task)
    {
        task.Status = DownloadStatus.Installing;
        task.IsIndeterminate = true;
        NotifyDownloadStatus(task);
        Task.Run(() => { }); // installing
    }

    private void DoDownload(DownloadItem task)
    {
        if (task._service != null)
        {
            task._service.Resume();
            return;
        }

        task._service = new DownloadService();
        task._service.DownloadFileTaskAsync(task.Resource.DownloadURL, GetDownloadingFileName(task.Resource));
        task.Status = DownloadStatus.Downloading;
        NotifyDownloadStatus(task);

        task._service.DownloadFileCompleted += (sender, args) =>
        {
            DoInstall(task);
            lock (_tasks)
            {
                task.Status = DownloadStatus.Done;
                NotifyDownloadStatus(task);
                Monitor.Pulse(_tasks);
            }
        };
        task._service.DownloadProgressChanged += (sender, args) =>
        {
            task.Finished = args.ReceivedBytesSize;
            task.Total = args.TotalBytesToReceive;
            task.Speed = args.BytesPerSecondSpeed;
            NotifyDownloadStatus(task);
        };
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
    private static Lazy<DownloadManager> _instance = new();
    public static DownloadManager Instance => _instance.Value;
}