using System.Text.Json.Serialization;

namespace TMSpeech.Core.Services.Resource;

public class ModuleInfo
{
    [JsonPropertyName("id")]
    public string ID { get; set; }

    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    [JsonPropertyName("updateDesc")]
    public string? UpdateDesc { get; set; }

    [JsonPropertyName("displayVersion")]
    public string DisplayVersion { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    [JsonPropertyName("repository")]
    public string? Repository { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// only for plugin
    /// </summary>
    [JsonPropertyName("apiLevel")]
    public int? ApiLevel { get; set; }

    /// <summary>
    /// only for plugin
    /// </summary>
    [JsonPropertyName("assemblies")]
    public List<string>? Assemblies { get; set; }

    [JsonPropertyName("sherpaonnx")]
    public SherpaOnnxModelPathInfo? SherpaOnnxModelPath { get; set; }

    [JsonPropertyName("install")]
    public IList<InstallStep>? InstallSteps { get; set; }
}

public class SherpaOnnxModelPathInfo
{
    [JsonPropertyName("encoder")]
    public string EncoderPath { get; set; }

    [JsonPropertyName("decoder")]
    public string DocoderPath { get; set; }

    [JsonPropertyName("joiner")]
    public string JoinerPath { get; set; }

    [JsonPropertyName("token")]
    public string TokenPath { get; set; }
}

public class InstallStep
{
    /// <summary>
    /// enum of
    /// download, extract, write_file, write_module_json
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("url")]
    public string? DownloadURL { get; set; }

    [JsonPropertyName("extractStep")]
    public int? ExtractStep { get; set; }
    
    [JsonPropertyName("extractType")]
    public string ExtractType { get; set; }

    [JsonPropertyName("writeContent")]
    public string? WriteContent { get; set; }

    [JsonPropertyName("writePath")]
    public string? WritePath { get; set; }

    [JsonPropertyName("extractTo")]
    public string? ExtractTo { get; set; }
}