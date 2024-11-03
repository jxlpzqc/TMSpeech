using System.Text.Json;
using System.Text.RegularExpressions;
using Downloader;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace TMSpeech.Core.Services.Resource;

public enum DownloadStatus
{
    Idle,
    Pending,
    Downloading,
    Installing,
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

    public Exception? FailReason { get; set; }

    internal int _step = 0;
    internal DownloadService? _service;
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
            if (!_tasks.ContainsKey(resource.ID)) return;
            DownloadItem task = _tasks[resource.ID];

            if (task.Status == DownloadStatus.Paused && task._service != null)
            {
                task.Status = DownloadStatus.Downloading;
                NotifyDownloadStatus(task);
                task._service.Resume();
                return;
            }

            if (task.Status != DownloadStatus.Idle && task.Status != DownloadStatus.Failed) return;

            lock (_tasks)
            {
                task.Status = DownloadStatus.Pending;
                NotifyDownloadStatus(task);

                while (_tasks.Count(u => u.Value.IsWorking) >= _maxDownloadSize)
                {
                    Monitor.Wait(_tasks);
                }
            }

            DoJob(task);
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
            NotifyDownloadStatus(task);
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

        var dir = Path.Combine(ConfigManagerFactory.Instance.UserDataDir, "plugins", res.ID);
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

            var archiveFilePath = GetDownloadingFileName(task.Resource, fileStep);

            var extractDest = string.IsNullOrEmpty(currrentStep.ExtractTo)
                ? GetPluginDirName(task.Resource)
                : Path.Combine(GetPluginDirName(task.Resource), currrentStep.ExtractTo);

            // var newPath = archiveFilePath + ".tar.bz2";
            // File.Move(archiveFilePath, newPath);
            // using var archive = ArchiveFactory.Open(newPath);

            using var stream = File.OpenRead(archiveFilePath);
            using var reader = ReaderFactory.Open(stream);

            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    var filename = reader.Entry.Key;
                    filename = Path.Combine(extractDest, filename);
                    var dir = Path.GetDirectoryName(filename);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    reader.WriteEntryToFile(filename, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }


            // foreach (var entry in archive.Entries.Where(u => !u.IsDirectory))
            // {
            //     entry.WriteToDirectory(extractDest, new ExtractionOptions
            //     {
            //         ExtractFullPath = true,
            //         Overwrite = true
            //     });
            // }
        });
    }

    private async Task DoWriteFile(DownloadItem task)
    {
        task.IsIndeterminate = true;
        UpdateJobStatus(task, DownloadStatus.Installing);
        var currrentStep = task.Resource.ModuleInfo.InstallSteps[task._step];
        File.WriteAllText(currrentStep.WritePath, currrentStep.WriteContent);
    }

    private void UpdateJobStatus(DownloadItem task, DownloadStatus newStatus, Exception ex = null)
    {
        lock (_tasks)
        {
            task.Status = newStatus;
            task.FailReason = ex;
            NotifyDownloadStatus(task);
            Monitor.Pulse(_tasks);
        }
    }

    private async Task DoJob(DownloadItem task)
    {
        var installsteps = task.Resource.ModuleInfo.InstallSteps;
        if (installsteps == null)
        {
            UpdateJobStatus(task, DownloadStatus.Idle);
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
                catch (Exception e)
                {
                    UpdateJobStatus(task, DownloadStatus.Failed, e);
                    return;
                }
            }
            else if (step.Type == "extract")
            {
                try
                {
                    await DoExtract(task);
                }
                catch (Exception e)
                {
                    UpdateJobStatus(task, DownloadStatus.Failed, e);
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
                    UpdateJobStatus(task, DownloadStatus.Failed, e);
                    return;
                }
            }
            else if (step.Type == "write_module_json")
            {
                try
                {
                    var jsonFileName = Path.Combine(GetPluginDirName(task.Resource), "tmmodule.json");
                    File.WriteAllText(jsonFileName, JsonSerializer.Serialize(task.Resource.ModuleInfo));
                }
                catch (Exception e)
                {
                    UpdateJobStatus(task, DownloadStatus.Failed, e);
                    return;
                }
            }
        }

        await task.Resource.UpdateLocal();
        task._step = 0;
        UpdateJobStatus(task, DownloadStatus.Idle);
        Directory.Delete(Path.Combine(GetPluginDirName(task.Resource), "downloading"), true);
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
                UpdateJobStatus(task, DownloadStatus.Failed, args.Error);
            }

            task._service = null;
            t.SetResult();
        };
        task._service.DownloadProgressChanged += (sender, args) =>
        {
            task.Finished = args.ReceivedBytesSize;
            task.Total = args.TotalBytesToReceive;
            task.Speed = args.BytesPerSecondSpeed;
            NotifyDownloadStatus(task);
        };

        var downloadUrl = task.Resource.ModuleInfo.InstallSteps[task._step].DownloadURL;

        task._service.DownloadFileTaskAsync(downloadUrl,
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

    public void UpdateItem(Resource res)
    {
        if (_tasks.ContainsKey(res.ID)) _tasks[res.ID].Resource = res;
    }
}

public static class DownloadManagerFactory
{
    private static Lazy<DownloadManager> _instance = new(() => new DownloadManager());
    public static DownloadManager Instance => _instance.Value;
}