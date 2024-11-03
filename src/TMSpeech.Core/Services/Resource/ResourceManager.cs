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
    public bool IsPlugin => ModuleInfo.Type == "plugin";
    public bool IsLocal => LocalInfo != null;
    public bool NeedUpdate => (RemoteInfo != null && LocalInfo != null) && RemoteInfo.Version > LocalInfo.Version;

    public void UpdateLocal()
    {
        const string pluginDirName = "plugins";
        var execuatblePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), pluginDirName);
        var userdatadir = Path.Combine(ConfigManagerFactory.Instance.UserDataDir, pluginDirName);

        ModuleInfo? resultInfo = null;
        string resultDir = null;
        bool resultCanremove = false;

        foreach (var (canremove, dir) in Directory.GetDirectories(execuatblePath).Select(u => (false, u))
                     .Concat(Directory.GetDirectories(userdatadir).Select(u => (true, u))))
        {
            var jsonFileName = Path.Combine(dir, "tmmodule.json");
            if (File.Exists(jsonFileName))
            {
                string moduleJson = File.ReadAllText(jsonFileName);
                ModuleInfo moduleInfo;
                try
                {
                    moduleInfo = JsonSerializer.Deserialize<ModuleInfo>(moduleJson);
                }
                catch
                {
                    Debug.WriteLine($"Fail to parse json file at {dir}");
                    continue;
                }

                if (moduleInfo.ID == ID)
                {
                    resultInfo = moduleInfo;
                    resultDir = dir;
                    resultCanremove = canremove;
                }
            }
        }

        LocalInfo = resultInfo;
        LocalDir = resultDir;
        CanRemove = resultCanremove;
    }
}

public class ResourceManager
{
    public async Task<IList<Resource>> GetLocalResources()
    {
        const string pluginDirName = "plugins";
        var execuatblePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), pluginDirName);
        var userdatadir = Path.Combine(ConfigManagerFactory.Instance.UserDataDir, pluginDirName);
        if (!Directory.Exists(execuatblePath)) Directory.CreateDirectory(execuatblePath);
        if (!Directory.Exists(userdatadir)) Directory.CreateDirectory(userdatadir);

        List<Resource> ret = new List<Resource>();

        foreach (var (canremove, dir) in Directory.GetDirectories(execuatblePath).Select(u => (false, u))
                     .Concat(Directory.GetDirectories(userdatadir).Select(u => (true, u))))
        {
            var jsonFileName = Path.Combine(dir, "tmmodule.json");
            if (File.Exists(jsonFileName))
            {
                string moduleJson = File.ReadAllText(jsonFileName);
                ModuleInfo moduleInfo;
                try
                {
                    moduleInfo = JsonSerializer.Deserialize<ModuleInfo>(moduleJson);
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
                    CanRemove = canremove
                });
            }
        }

        return ret;
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

    public async Task<IList<Resource>> GetRemoteResources()
    {
        var client = new HttpClient();
        var json = await client.GetStringAsync("https://chengzi.tech/TMSpeechCommunity/marketplace.json");
        var marketPlace = JsonSerializer.Deserialize<MarketPlace>(json);
        var modules = marketPlace?.Modules;
        return modules?.Select(u => new Resource { RemoteInfo = u }).ToList() ?? new List<Resource>();
    }

    public async Task<IList<Resource>> GetAllResources()
    {
        var local = await GetLocalResources();
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