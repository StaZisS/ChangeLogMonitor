using YamlDotNet.Serialization;

namespace ChangeLogMonitor.Configuration.Models;

public class YamlFieldPolicy
{
    [YamlMember(Alias = "action")] public string? Action { get; set; }

    [YamlMember(Alias = "view")] public YamlViewSettings? View { get; set; }

    [YamlMember(Alias = "mask")] public YamlMaskSettings? Mask { get; set; }

    [YamlMember(Alias = "hash")] public YamlHashSettings? Hash { get; set; }

    [YamlMember(Alias = "encrypt")] public YamlEncryptSettings? Encrypt { get; set; }
}

public class YamlViewSettings
{
    [YamlMember(Alias = "format")] public string? Format { get; set; }

    [YamlMember(Alias = "pattern")] public string? Pattern { get; set; }

    [YamlMember(Alias = "culture")] public string? Culture { get; set; }

    [YamlMember(Alias = "enumLabel")] public bool? EnumLabel { get; set; }

    [YamlMember(Alias = "refName")] public bool? RefName { get; set; }

    [YamlMember(Alias = "enumType")] public string? EnumType { get; set; }
}

public class YamlMaskSettings
{
    [YamlMember(Alias = "preset")] public string? Preset { get; set; }

    [YamlMember(Alias = "char")] public string? Char { get; set; }

    [YamlMember(Alias = "keepLeft")] public int? KeepLeft { get; set; }

    [YamlMember(Alias = "keepRight")] public int? KeepRight { get; set; }

    [YamlMember(Alias = "preserveDomain")] public bool? PreserveDomain { get; set; }

    [YamlMember(Alias = "preserveFormat")] public bool? PreserveFormat { get; set; }

    [YamlMember(Alias = "regex")] public string? Regex { get; set; }

    [YamlMember(Alias = "replace")] public string? Replace { get; set; }
}

public class YamlHashSettings
{
    [YamlMember(Alias = "preset")] public string? Preset { get; set; }

    [YamlMember(Alias = "algo")] public string? Algo { get; set; }

    [YamlMember(Alias = "salt")] public YamlSaltSettings? Salt { get; set; }

    [YamlMember(Alias = "pepperRef")] public string? PepperRef { get; set; }

    [YamlMember(Alias = "encoding")] public string? Encoding { get; set; }

    [YamlMember(Alias = "storeRaw")] public bool? StoreRaw { get; set; }

    [YamlMember(Alias = "storeHash")] public bool? StoreHash { get; set; }

    [YamlMember(Alias = "equalityToken")] public bool? EqualityToken { get; set; }
}

public class YamlSaltSettings
{
    [YamlMember(Alias = "strategy")] public string? Strategy { get; set; }

    [YamlMember(Alias = "ref")] public string? Ref { get; set; }
}

public class YamlEncryptSettings
{
    [YamlMember(Alias = "preset")] public string? Preset { get; set; }

    [YamlMember(Alias = "algo")] public string? Algo { get; set; }

    [YamlMember(Alias = "keyRef")] public string? KeyRef { get; set; }

    [YamlMember(Alias = "aad")] public List<string>? Aad { get; set; }

    [YamlMember(Alias = "iv")] public YamlIvSettings? Iv { get; set; }

    [YamlMember(Alias = "encoding")] public string? Encoding { get; set; }

    [YamlMember(Alias = "storeRaw")] public bool? StoreRaw { get; set; }

    [YamlMember(Alias = "rotate")] public YamlRotateSettings? Rotate { get; set; }
}

public class YamlIvSettings
{
    [YamlMember(Alias = "strategy")] public string? Strategy { get; set; }

    [YamlMember(Alias = "store")] public bool? Store { get; set; }

    [YamlMember(Alias = "length")] public int? Length { get; set; }
}

public class YamlRotateSettings
{
    [YamlMember(Alias = "enabled")] public bool? Enabled { get; set; }

    [YamlMember(Alias = "policy")] public string? Policy { get; set; }
}