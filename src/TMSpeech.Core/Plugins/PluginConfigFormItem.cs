namespace TMSpeech.Core.Plugins;

public abstract record PluginConfigFormItem(
    string Key,
    string Name,
    string Description = ""
);

public record PluginConfigFormItemText(
    string Key,
    string Name,
    string Description = "",
    string Placeholder = ""
) : PluginConfigFormItem(Key, Name, Description);

public record PluginConfigFormItemOption(
    string Key,
    string Name,
    IList<string> Options,
    string Description = ""
) : PluginConfigFormItem(Key, Name, Description);

public enum PluginConfigFormItemFileType
{
    File,
    Folder
}

public record PluginConfigFormItemFile(
    string Key,
    string Name,
    string Description = "",
    PluginConfigFormItemFileType Type = PluginConfigFormItemFileType.File,
    string Filter = "",
    string? DefaultPath = null
) : PluginConfigFormItem(Key, Name, Description);

public record PluginConfigFormItemFolder(
    string Key,
    string Name,
    string Description = "",
    string? DefaultPath = null
) : PluginConfigFormItem(Key, Name, Description);

public record PluginConfigFormItemPassword(
    string Key,
    string Name,
    string Description = ""
) : PluginConfigFormItem(Key, Name, Description);

public record PluginConfigFormItemNumber(
    string Key,
    string Name,
    string Description = "",
    int? Min = null,
    int? Max = null,
    bool IsInteger = true
) : PluginConfigFormItem(Key, Name, Description);

public record PluginConfigFormCheckBox(
    string Key,
    string Name,
    string Description = ""
) : PluginConfigFormItem(Key, Name, Description);

public record PluginConfigFormItemColor(
    string Key,
    string Name,
    string Description = ""
) : PluginConfigFormItem(Key, Name, Description);

public record PluginConfigFormItemMessage(
    string Message
) : PluginConfigFormItem("", "", "");