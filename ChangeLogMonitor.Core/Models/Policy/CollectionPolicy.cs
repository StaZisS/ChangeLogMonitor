namespace ChangeLogMonitor.Core.Models.Policy;

/// <summary>
/// Политика для коллекции (навигационное свойство)
/// </summary>
public class CollectionPolicy
{
    public string? Preset { get; set; }

    public bool LogDeltas { get; set; } = true;

    public bool ShowKeys { get; set; } = true;

    public bool ShowNames { get; set; } = true;

    public string? ItemKeySelector { get; set; }

    public string? ItemNameSelector { get; set; }

    public string ItemViewTemplate { get; set; } = "{name} (ID={key})";

    public DeltaViewSettings DeltaView { get; set; } = new();

    public CollectionLimits Limits { get; set; } = new();

    public CountOnlySettings CountOnlyWhenLarge { get; set; } = new();

    public bool TrackReordering { get; set; }

    public string IncludeOnCreate { get; set; } = "none";

    public string IncludeOnDelete { get; set; } = "none";

    public bool MembershipKeysSensitive { get; set; }

    public M2MSettings? M2M { get; set; }
}

public class DeltaViewSettings
{
    public string AddedPrefix { get; set; } = "Добавлено:";

    public string RemovedPrefix { get; set; } = "Удалено:";

    public string Joiner { get; set; } = ", ";

    public bool CollapseToCounters { get; set; }
}

public class CollectionLimits
{
    public int AddedMax { get; set; } = 200;

    public int RemovedMax { get; set; } = 200;
}

public class CountOnlySettings
{
    public bool Enabled { get; set; } = true;

    public int Threshold { get; set; } = 2000;
}

public class M2MSettings
{
    public string? JoinEntity { get; set; }

    public List<string> JoinFields { get; set; } = new();

    public bool TreatJoinCudAsMembership { get; set; } = true;
}

/// <summary>
/// Умолчания для коллекций
/// </summary>
public class CollectionDefaults
{
    public bool LogDeltas { get; set; } = true;

    public bool ShowKeys { get; set; } = true;

    public bool ShowNames { get; set; } = true;

    public string ItemViewTemplate { get; set; } = "{name} (ID={key})";

    public CollectionLimits Limits { get; set; } = new();
}
