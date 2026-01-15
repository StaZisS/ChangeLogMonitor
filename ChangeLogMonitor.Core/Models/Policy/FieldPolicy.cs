using ChangeLogMonitor.Core.Enums;

namespace ChangeLogMonitor.Core.Models.Policy;

/// <summary>
///     Политика для конкретного поля
/// </summary>
public class FieldPolicy
{
    public FieldAction Action { get; set; } = FieldAction.Include;

    // Настройки view (форматирование)
    public ViewSettings? View { get; set; }

    // Настройки маскирования
    public MaskSettings? Mask { get; set; }

    // Настройки хеширования
    public HashSettings? Hash { get; set; }

    // Настройки шифрования
    public EncryptSettings? Encrypt { get; set; }
}

/// <summary>
///     Настройки отображения (view) для поля
/// </summary>
public class ViewSettings
{
    public FieldType? Format { get; set; }

    public string? Pattern { get; set; }

    public string? Culture { get; set; }

    public bool EnumLabel { get; set; } = true;

    public bool RefName { get; set; } = true;

    /// <summary>
    ///     Имя типа enum для связи со снепшотами enum значений.
    ///     Используется когда Format = Enum для получения человекочитаемых лейблов.
    /// </summary>
    public string? EnumType { get; set; }
}

/// <summary>
///     Настройки маскирования
/// </summary>
public class MaskSettings
{
    public string? Preset { get; set; }

    public char MaskChar { get; set; } = '*';

    public int KeepLeft { get; set; }

    public int KeepRight { get; set; }

    public bool PreserveDomain { get; set; }

    public bool PreserveFormat { get; set; } = true;

    public string? Regex { get; set; }

    public string? Replace { get; set; }
}

/// <summary>
///     Настройки хеширования
/// </summary>
public class HashSettings
{
    public string? Preset { get; set; }

    public string Algo { get; set; } = "SHA-256";

    public SaltSettings? Salt { get; set; }

    public string? PepperRef { get; set; }

    public string Encoding { get; set; } = "base64";

    public bool StoreRaw { get; set; }

    public bool StoreHash { get; set; } = true;

    public bool EqualityToken { get; set; }
}

public class SaltSettings
{
    public string Strategy { get; set; } = "per-record";

    public string? Ref { get; set; }
}

/// <summary>
///     Настройки шифрования
/// </summary>
public class EncryptSettings
{
    public string? Preset { get; set; }

    public string Algo { get; set; } = "AES-256-GCM";

    public string? KeyRef { get; set; }

    public List<string> Aad { get; set; } = new();

    public IvSettings Iv { get; set; } = new();

    public string Encoding { get; set; } = "base64";

    public bool StoreRaw { get; set; }

    public RotateSettings? Rotate { get; set; }
}

public class IvSettings
{
    public string Strategy { get; set; } = "random";

    public bool Store { get; set; } = true;

    public int Length { get; set; } = 12;
}

public class RotateSettings
{
    public bool Enabled { get; set; }

    public string Policy { get; set; } = "by-key-alias";
}