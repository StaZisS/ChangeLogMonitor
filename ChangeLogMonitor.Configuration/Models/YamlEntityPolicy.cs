using YamlDotNet.Serialization;

namespace ChangeLogMonitor.Configuration.Models;

public class YamlEntityPolicy
{
    [YamlMember(Alias = "enabled")] public bool? Enabled { get; set; }

    [YamlMember(Alias = "displayName")] public string? DisplayName { get; set; }

    [YamlMember(Alias = "onCreate")] public string? OnCreate { get; set; }

    [YamlMember(Alias = "onUpdate")] public string? OnUpdate { get; set; }

    [YamlMember(Alias = "onDelete")] public string? OnDelete { get; set; }

    [YamlMember(Alias = "fields")] public Dictionary<string, object>? Fields { get; set; }

    [YamlMember(Alias = "references")] public Dictionary<string, YamlReferencePolicy>? References { get; set; }

    [YamlMember(Alias = "collections")] public Dictionary<string, YamlCollectionPolicy>? Collections { get; set; }

    [YamlMember(Alias = "access")] public YamlEntityAccess? Access { get; set; }
}