namespace ChangeLogMonitor.Core.Models.Policy;

/// <summary>
/// Пресет для маскирования
/// </summary>
public class MaskPreset
{
    public char MaskChar { get; set; } = '*';

    public int KeepLeft { get; set; }

    public int KeepRight { get; set; }

    public bool PreserveDomain { get; set; }

    public bool PreserveFormat { get; set; } = true;
}

/// <summary>
/// Пресет для хеширования
/// </summary>
public class HashPreset
{
    public string Algo { get; set; } = "SHA-256";

    public SaltSettings? Salt { get; set; }

    public string? PepperRef { get; set; }

    public string Encoding { get; set; } = "base64";

    public bool StoreRaw { get; set; }

    public bool StoreHash { get; set; } = true;

    public bool EqualityToken { get; set; }
}

/// <summary>
/// Пресет для шифрования
/// </summary>
public class EncryptPreset
{
    public string Algo { get; set; } = "AES-256-GCM";

    public string? KeyRef { get; set; }

    public List<string> Aad { get; set; } = new();

    public IvSettings Iv { get; set; } = new();

    public string Encoding { get; set; } = "base64";

    public bool StoreRaw { get; set; }
}

/// <summary>
/// Пресет для ссылок
/// </summary>
public class ReferencePreset
{
    public bool ShowKey { get; set; } = true;

    public bool ShowName { get; set; } = true;

    public string ViewTemplate { get; set; } = "{name} (ID={key})";

    public string? NameMaskPreset { get; set; }
}

/// <summary>
/// Пресет для коллекций
/// </summary>
public class CollectionPreset
{
    public bool LogDeltas { get; set; } = true;

    public bool ShowKeys { get; set; }

    public bool ShowNames { get; set; } = true;

    public string ItemViewTemplate { get; set; } = "{name} (ID={key})";

    public bool CollapseToCounters { get; set; }
}
