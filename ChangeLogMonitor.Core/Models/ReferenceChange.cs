namespace ChangeLogMonitor.Core.Models;

/// <summary>
///     Изменение FK/ссылки на другую сущность
/// </summary>
public class ReferenceChange
{
    public string ReferenceName { get; set; } = string.Empty;

    // Ключи (ID связанных объектов)
    public string? OldKey { get; set; }

    public string? NewKey { get; set; }

    // Денормализованные имена (на момент изменения)
    public string? OldName { get; set; }

    public string? NewName { get; set; }

    // Форматированные значения для UI (шаблон: "{name} (ID={key})")
    public string? ViewOldValue { get; set; }

    public string? ViewNewValue { get; set; }

    // Метаданные
    public bool IsNameResolved { get; set; }

    public string NameFallback { get; set; } = "{key}";
}