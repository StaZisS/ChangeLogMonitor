using ChangeLogMonitor.Core.Enums;

namespace ChangeLogMonitor.Core.Models;

/// <summary>
///     Изменение обычного поля сущности
/// </summary>
public class FieldChange
{
    public string FieldName { get; set; } = string.Empty;

    public string? RawOldValue { get; set; }

    public string? RawNewValue { get; set; }

    public string? ViewOldValue { get; set; }

    public string? ViewNewValue { get; set; }

    public FieldType FieldType { get; set; }

    public bool IsSensitive { get; set; }

    public bool IsMasked { get; set; }

    public bool IsEncrypted { get; set; }

    public bool IsHashed { get; set; }

    public string? EnumLabel { get; set; }
}