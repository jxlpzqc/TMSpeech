using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TMSpeech.Core.Services.Resource;

/// <summary>
/// Represent a dynamic resource
/// </summary>
///
/// <remarks>
/// <c>LocalInfo</c> and <c>RemoteInfo</c> could not both be null
/// </remarks>
public class Resource
{
    public bool CanRemove { get; set; } = true;
    public ModuleInfo? LocalInfo { get; set; }
    public string LocalDir { get; set; }
    public ModuleInfo? RemoteInfo { get; set; }

    public ModuleInfo ModuleInfo => (RemoteInfo ?? LocalInfo)!;
    public string ID => ModuleInfo.ID;
    public string Name => ModuleInfo.Name;
    public string Desc => ModuleInfo.Desc;
    public bool IsPlugin => ModuleInfo.Type == ModuleInfoTypeEnums.Plugin;
    public bool IsLocal => LocalInfo != null;
    public bool NeedUpdate => (RemoteInfo != null && LocalInfo != null) && RemoteInfo.Version > LocalInfo.Version;

    public void UpdateLocalSync()
    {
        UpdateLocal().Wait();
    }

    public async Task UpdateLocal()
    {
        var res = await ResourceManagerFactory.Instance.GetLocalResource(ID, true);
        if (res == null) return;
        LocalInfo = res.LocalInfo;
        LocalDir = res.LocalDir;
        CanRemove = res.CanRemove;
    }
}

public class ResourceManager
{
    const string PluginDirName = "plugins";
    const string ModuleJsonFileName = "tmmodule.json";

    private IList<Resource>? _localCache;
    private IDictionary<string, Resource>? _localCacheDict;

    public async Task<Resource?> GetLocalResource(string id, bool dismissCache = false)
    {
        await GetLocalResources(dismissCache);
        return _localCacheDict?[id];
    }

    public async Task<IList<ModuleInfo>> GetLocalModuleInfos()
    {
        return (await GetLocalResources()).Select(u => u.ModuleInfo).ToList();
    }

    public async Task<ModuleInfo?> GetLocalModuleInfo(string id, bool dismissCache = false)
    {
        return (await GetLocalResource(id, dismissCache))?.LocalInfo;
    }

    public async Task<IList<Resource>> GetLocalResources(bool dismissCache = false)
    {
        if (_localCache == null || dismissCache)
        {
            // TODO: concurrency problem?
            await RealGetLocalResources();
        }

        return _localCache;
    }

    private async Task RealGetLocalResources()
    {
        var execuatblePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), PluginDirName);
        var userdatadir = Path.Combine(ConfigManagerFactory.Instance.UserDataDir, PluginDirName);
        if (!Directory.Exists(execuatblePath)) Directory.CreateDirectory(execuatblePath);
        if (!Directory.Exists(userdatadir)) Directory.CreateDirectory(userdatadir);

        List<Resource> ret = new List<Resource>();

        foreach (var (canRemove, dir) in Directory.GetDirectories(execuatblePath).Select(u => (false, u))
                     .Concat(Directory.GetDirectories(userdatadir).Select(u => (true, u))))
        {
            var jsonFileName = Path.Combine(dir, ModuleJsonFileName);
            if (File.Exists(jsonFileName))
            {
                ModuleInfo? moduleInfo;
                try
                {
                    moduleInfo = await JsonSerializer.DeserializeAsync<ModuleInfo>(File.OpenRead(jsonFileName));
                }
                catch
                {
                    Debug.WriteLine($"Fail to parse json file at {dir}");
                    continue;
                }

                ret.Add(new Resource
                {
                    LocalInfo = moduleInfo,
                    LocalDir = dir,
                    CanRemove = canRemove
                });
            }
        }

        _localCache = ret;
        _localCacheDict = ret.ToDictionary(x => x.ID, x => x);
    }

    public async Task RemoveResource(Resource resource)
    {
        if (!resource.CanRemove || resource.LocalDir == null) return;
        Directory.Delete(resource.LocalDir, true);
    }

    private class MarketPlace
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("modules")]
        public IList<ModuleInfo> Modules { get; set; }
    }

    private async Task<IList<Resource>> GetRemoteResources()
    {
        var client = new HttpClient();
        var json = await client.GetStringAsync("https://chengzi.tech/TMSpeechCommunity/marketplace.json");
        var marketPlace = JsonSerializer.Deserialize<MarketPlace>(json);
        var modules = marketPlace?.Modules;
        return modules?.Select(u => new Resource { RemoteInfo = u }).ToList() ?? new List<Resource>();
    }

    public async Task<IList<Resource>> GetAllResources()
    {
        var local = (await GetLocalResources(true)).ToList();
        var localDict = local.ToDictionary(x => x.ID, x => x);
        var remote = await GetRemoteResources();
        foreach (var r in remote)
        {
            if (localDict.ContainsKey(r.ID))
            {
                localDict[r.ID].RemoteInfo = r.RemoteInfo;
            }
            else
            {
                local.Add(r);
            }
        }

        return local;
    }

    internal ResourceManager()
    {
    }
}

public static class ResourceManagerFactory
{
    private static Lazy<ResourceManager> _manager = new(() => new ResourceManager());

    public static ResourceManager Instance => _manager.Value;
}