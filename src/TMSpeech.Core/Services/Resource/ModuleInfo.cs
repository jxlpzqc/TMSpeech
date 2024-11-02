using System.Text.Json.Serialization;

namespace TMSpeech.Core.Services.Resource;

public class ModuleInfo
{
    [JsonPropertyName("id")]
    public string ID { get; set; }

    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("desc")]
    public string Desc { get; set; }

    [JsonPropertyName("updateDesc")]
    public string UpdateDesc { get; set; }

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

    [JsonPropertyName("InstallStep")]
    public IList<InstallStep>? InstallSteps { get; set; }
}

public class SherpaOnnxModelPathInfo
{
    public string EncoderPath { get; set; }
    public string DocoderPath { get; set; }
    public string JoinerPath { get; set; }
    public string TokenPath { get; set; }
}

public class InstallStep
{
    /// <summary>
    /// enum of
    /// download, extract, write_file
    /// </summary>
    public string Type { get; set; }

    public string? DownloadURL { get; set; }

    public int? ExtractStep { get; set; }

    public string? WriteContent { get; set; }

    public string? WritePath { get; set; }
    public string? ExtractTo { get; set; }
}