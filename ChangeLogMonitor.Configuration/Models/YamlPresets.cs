using YamlDotNet.Serialization;

namespace ChangeLogMonitor.Configuration.Models;

public class YamlMaskPreset
{
    [YamlMember(Alias = "char")] public string? Char { get; set; }

    [YamlMember(Alias = "keepLeft")] public int? KeepLeft { get; set; }

    [YamlMember(Alias = "keepRight")] public int? KeepRight { get; set; }

    [YamlMember(Alias = "preserveDomain")] public bool? PreserveDomain { get; set; }

    [YamlMember(Alias = "preserveFormat")] public bool? PreserveFormat { get; set; }
}

public class YamlHashPreset
{
    [YamlMember(Alias = "algo")] public string? Algo { get; set; }

    [YamlMember(Alias = "salt")] public YamlSaltSettings? Salt { get; set; }

    [YamlMember(Alias = "pepperRef")] public string? PepperRef { get; set; }

    [YamlMember(Alias = "encoding")] public string? Encoding { get; set; }

    [YamlMember(Alias = "storeRaw")] public bool? StoreRaw { get; set; }

    [YamlMember(Alias = "storeHash")] public bool? StoreHash { get; set; }

    [YamlMember(Alias = "equalityToken")] public bool? EqualityToken { get; set; }
}

public class YamlEncryptPreset
{
    [YamlMember(Alias = "algo")] public string? Algo { get; set; }

    [YamlMember(Alias = "keyRef")] public string? KeyRef { get; set; }

    [YamlMember(Alias = "aad")] public List<string>? Aad { get; set; }

    [YamlMember(Alias = "iv")] public YamlIvSettings? Iv { get; set; }

    [YamlMember(Alias = "encoding")] public string? Encoding { get; set; }

    [YamlMember(Alias = "storeRaw")] public bool? StoreRaw { get; set; }
}

public class YamlReferencePreset
{
    [YamlMember(Alias = "showKey")] public bool? ShowKey { get; set; }

    [YamlMember(Alias = "showName")] public bool? ShowName { get; set; }

    [YamlMember(Alias = "viewTemplate")] public string? ViewTemplate { get; set; }

    [YamlMember(Alias = "nameMaskPreset")] public string? NameMaskPreset { get; set; }
}

public class YamlCollectionPreset
{
    [YamlMember(Alias = "logDeltas")] public bool? LogDeltas { get; set; }

    [YamlMember(Alias = "showKeys")] public bool? ShowKeys { get; set; }

    [YamlMember(Alias = "showNames")] public bool? ShowNames { get; set; }

    [YamlMember(Alias = "itemViewTemplate")]
    public string? ItemViewTemplate { get; set; }

    [YamlMember(Alias = "collapseToCounters")]
    public bool? CollapseToCounters { get; set; }
}