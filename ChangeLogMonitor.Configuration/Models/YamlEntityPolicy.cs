using YamlDotNet.Serialization;

namespace ChangeLogMonitor.Configuration.Models;

/// <summary>
///     Политика для сущности (YAML)
/// </summary>
public class YamlEntityPolicy
{
    [YamlMember(Alias = "enabled")] public bool? Enabled { get; set; }

    [YamlMember(Alias = "onCreate")] public string? OnCreate { get; set; }

    [YamlMember(Alias = "onUpdate")] public string? OnUpdate { get; set; }

    [YamlMember(Alias = "onDelete")] public string? OnDelete { get; set; }

    [YamlMember(Alias = "fields")] public Dictionary<string, object>? Fields { get; set; }

    [YamlMember(Alias = "references")] public Dictionary<string, YamlReferencePolicy>? References { get; set; }

    [YamlMember(Alias = "collections")] public Dictionary<string, YamlCollectionPolicy>? Collections { get; set; }
}