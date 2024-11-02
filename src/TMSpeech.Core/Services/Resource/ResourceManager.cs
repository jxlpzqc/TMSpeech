using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMSpeech.Core.Services.Resource;

public class Resource
{
    public string ID { get; set; }
    public string Name { get; set; }
    public string Desc { get; set; }
    // TODO: is local
    public bool IsLocal => false;
    public bool NeedUpdate => false;
    public string DownloadURL { get; set; }
}

public class SherpaOnnxModelResource : Resource
{
    public string EncoderPath { get; set; }
    public string DocoderPath { get; set; }
    public string JoinerPath { get; set; }
    public string TokenPath { get; set; }
}

public class ResourceManager
{
    public async Task<IList<Resource>> GetLocalResources()
    {
        return new List<Resource>();
    }

    public async Task<IList<Resource>> GetRemoteResources()
    {
        return new List<Resource>();
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