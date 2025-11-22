using YamlDotNet.Serialization;

namespace ChangeLogMonitor.Configuration.Models;

public class YamlCollectionPolicy
{
    [YamlMember(Alias = "preset")] public string? Preset { get; set; }

    [YamlMember(Alias = "logDeltas")] public bool? LogDeltas { get; set; }

    [YamlMember(Alias = "showKeys")] public bool? ShowKeys { get; set; }

    [YamlMember(Alias = "showNames")] public bool? ShowNames { get; set; }

    [YamlMember(Alias = "itemKeySelector")]
    public string? ItemKeySelector { get; set; }

    [YamlMember(Alias = "itemNameSelector")]
    public string? ItemNameSelector { get; set; }

    [YamlMember(Alias = "itemViewTemplate")]
    public string? ItemViewTemplate { get; set; }

    [YamlMember(Alias = "deltaView")] public YamlDeltaViewSettings? DeltaView { get; set; }

    [YamlMember(Alias = "limits")] public YamlCollectionLimits? Limits { get; set; }

    [YamlMember(Alias = "countOnlyWhenLarge")]
    public YamlCountOnlySettings? CountOnlyWhenLarge { get; set; }

    [YamlMember(Alias = "trackReordering")]
    public bool? TrackReordering { get; set; }

    [YamlMember(Alias = "includeOnCreate")]
    public string? IncludeOnCreate { get; set; }

    [YamlMember(Alias = "includeOnDelete")]
    public string? IncludeOnDelete { get; set; }

    [YamlMember(Alias = "membershipKeysSensitive")]
    public bool? MembershipKeysSensitive { get; set; }

    [YamlMember(Alias = "m2m")] public YamlM2MSettings? M2M { get; set; }
}

public class YamlDeltaViewSettings
{
    [YamlMember(Alias = "addedPrefix")] public string? AddedPrefix { get; set; }

    [YamlMember(Alias = "removedPrefix")] public string? RemovedPrefix { get; set; }

    [YamlMember(Alias = "joiner")] public string? Joiner { get; set; }

    [YamlMember(Alias = "collapseToCounters")]
    public bool? CollapseToCounters { get; set; }
}

public class YamlCollectionLimits
{
    [YamlMember(Alias = "addedMax")] public int? AddedMax { get; set; }

    [YamlMember(Alias = "removedMax")] public int? RemovedMax { get; set; }
}

public class YamlCountOnlySettings
{
    [YamlMember(Alias = "enabled")] public bool? Enabled { get; set; }

    [YamlMember(Alias = "threshold")] public int? Threshold { get; set; }
}

public class YamlM2MSettings
{
    [YamlMember(Alias = "joinEntity")] public string? JoinEntity { get; set; }

    [YamlMember(Alias = "joinFields")] public List<string>? JoinFields { get; set; }

    [YamlMember(Alias = "treatJoinCudAsMembership")]
    public bool? TreatJoinCudAsMembership { get; set; }
}

public class YamlCollectionDefaults
{
    [YamlMember(Alias = "logDeltas")] public bool? LogDeltas { get; set; }

    [YamlMember(Alias = "showKeys")] public bool? ShowKeys { get; set; }

    [YamlMember(Alias = "showNames")] public bool? ShowNames { get; set; }

    [YamlMember(Alias = "itemViewTemplate")]
    public string? ItemViewTemplate { get; set; }

    [YamlMember(Alias = "limits")] public YamlCollectionLimits? Limits { get; set; }
}