namespace ChangeLogMonitor.Core.Models;

/// <summary>
///     Изменение FK/ссылки на другую сущность
/// </summary>
public class ReferenceChange
{
    public string ReferenceName { get; set; } = string.Empty;
    
    public string? OldKey { get; set; }

    public string? NewKey { get; set; }
    
    public string? OldName { get; set; }

    public string? NewName { get; set; }
    
    public string? ViewOldValue { get; set; }

    public string? ViewNewValue { get; set; }
    
    public bool IsNameResolved { get; set; }

    public string NameFallback { get; set; } = "{key}";
}