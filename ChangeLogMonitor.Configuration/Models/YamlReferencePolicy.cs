using YamlDotNet.Serialization;

namespace ChangeLogMonitor.Configuration.Models;

public class YamlReferencePolicy
{
    [YamlMember(Alias = "preset")] public string? Preset { get; set; }

    [YamlMember(Alias = "showKey")] public bool? ShowKey { get; set; }

    [YamlMember(Alias = "showName")] public bool? ShowName { get; set; }

    [YamlMember(Alias = "viewTemplate")] public string? ViewTemplate { get; set; }

    [YamlMember(Alias = "nameSelector")] public string? NameSelector { get; set; }

    [YamlMember(Alias = "nameResolve")] public YamlNameResolveSettings? NameResolve { get; set; }

    [YamlMember(Alias = "nameMaskPreset")] public string? NameMaskPreset { get; set; }

    [YamlMember(Alias = "key")] public YamlKeySensitivitySettings? Key { get; set; }

    [YamlMember(Alias = "nullTransitions")]
    public string? NullTransitions { get; set; }

    [YamlMember(Alias = "changedAs")] public string? ChangedAs { get; set; }
}

public class YamlNameResolveSettings
{
    [YamlMember(Alias = "stage")] public string? Stage { get; set; }

    [YamlMember(Alias = "fallback")] public string? Fallback { get; set; }

    [YamlMember(Alias = "maxLen")] public int? MaxLen { get; set; }
}

public class YamlKeySensitivitySettings
{
    [YamlMember(Alias = "treatAsSensitive")]
    public bool? TreatAsSensitive { get; set; }

    [YamlMember(Alias = "maskPreset")] public string? MaskPreset { get; set; }

    [YamlMember(Alias = "hashPreset")] public string? HashPreset { get; set; }
}

public class YamlReferenceDefaults
{
    [YamlMember(Alias = "showKey")] public bool? ShowKey { get; set; }

    [YamlMember(Alias = "showName")] public bool? ShowName { get; set; }

    [YamlMember(Alias = "viewTemplate")] public string? ViewTemplate { get; set; }

    [YamlMember(Alias = "nameResolve")] public YamlNameResolveSettings? NameResolve { get; set; }
}