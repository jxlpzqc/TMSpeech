using System.Text.Json.Serialization;

namespace TMSpeech.Core.Plugins;

public class ModuleInfo
{
    [JsonPropertyName("apiLevel")]
    public int ApiLevel { get; set; }

    [JsonPropertyName("assemblies")]
    public List<string>? Assemblies { get; set; }
}