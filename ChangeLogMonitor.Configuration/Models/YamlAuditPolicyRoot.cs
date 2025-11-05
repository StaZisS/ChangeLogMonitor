using YamlDotNet.Serialization;

namespace ChangeLogMonitor.Configuration.Models;

/// <summary>
/// Корневая YAML модель для десериализации changelog-config.yaml
/// </summary>
public class YamlAuditPolicyRoot
{
    [YamlMember(Alias = "auditPolicy")]
    public YamlAuditPolicy AuditPolicy { get; set; } = new();
}

/// <summary>
/// Главная политика аудита (YAML)
/// </summary>
public class YamlAuditPolicy
{
    [YamlMember(Alias = "version")]
    public string? Version { get; set; }

    [YamlMember(Alias = "mode")]
    public string? Mode { get; set; }

    [YamlMember(Alias = "onCreate")]
    public string? OnCreate { get; set; }

    [YamlMember(Alias = "onUpdate")]
    public string? OnUpdate { get; set; }

    [YamlMember(Alias = "onDelete")]
    public string? OnDelete { get; set; }

    [YamlMember(Alias = "globalFieldExclusions")]
    public List<string>? GlobalFieldExclusions { get; set; }

    [YamlMember(Alias = "methodPresets")]
    public YamlMethodPresets? MethodPresets { get; set; }

    [YamlMember(Alias = "referencePresets")]
    public Dictionary<string, YamlReferencePreset>? ReferencePresets { get; set; }

    [YamlMember(Alias = "collectionPresets")]
    public Dictionary<string, YamlCollectionPreset>? CollectionPresets { get; set; }

    [YamlMember(Alias = "referenceDefaults")]
    public YamlReferenceDefaults? ReferenceDefaults { get; set; }

    [YamlMember(Alias = "collectionDefaults")]
    public YamlCollectionDefaults? CollectionDefaults { get; set; }

    [YamlMember(Alias = "entities")]
    public Dictionary<string, YamlEntityPolicy>? Entities { get; set; }

    [YamlMember(Alias = "defaultCulture")]
    public string? DefaultCulture { get; set; }

    [YamlMember(Alias = "defaultTimeZone")]
    public string? DefaultTimeZone { get; set; }
}

/// <summary>
/// Пресеты для методов (mask, hash, encrypt)
/// </summary>
public class YamlMethodPresets
{
    [YamlMember(Alias = "mask")]
    public Dictionary<string, YamlMaskPreset>? Mask { get; set; }

    [YamlMember(Alias = "hash")]
    public Dictionary<string, YamlHashPreset>? Hash { get; set; }

    [YamlMember(Alias = "encrypt")]
    public Dictionary<string, YamlEncryptPreset>? Encrypt { get; set; }
}
