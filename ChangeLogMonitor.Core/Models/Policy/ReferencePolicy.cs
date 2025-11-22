namespace ChangeLogMonitor.Core.Models.Policy;

/// <summary>
///     Политика для ссылочного поля (FK)
/// </summary>
public class ReferencePolicy
{
    public string? Preset { get; set; }

    public bool ShowKey { get; set; } = true;

    public bool ShowName { get; set; } = true;

    public string ViewTemplate { get; set; } = "{name} (ID={key})";

    public string? NameSelector { get; set; }

    public NameResolveSettings NameResolve { get; set; } = new();

    public string? NameMaskPreset { get; set; }

    public KeySensitivitySettings Key { get; set; } = new();

    public string NullTransitions { get; set; } = "log";

    public string ChangedAs { get; set; } = "pair";
}

public class NameResolveSettings
{
    public string Stage { get; set; } = "normalization";

    public string Fallback { get; set; } = "{key}";

    public int MaxLen { get; set; } = 256;
}

public class KeySensitivitySettings
{
    public bool TreatAsSensitive { get; set; }

    public string? MaskPreset { get; set; }

    public string? HashPreset { get; set; }
}

/// <summary>
///     Умолчания для ссылок
/// </summary>
public class ReferenceDefaults
{
    public bool ShowKey { get; set; } = true;

    public bool ShowName { get; set; } = true;

    public string ViewTemplate { get; set; } = "{name} (ID={key})";

    public NameResolveSettings NameResolve { get; set; } = new();
}