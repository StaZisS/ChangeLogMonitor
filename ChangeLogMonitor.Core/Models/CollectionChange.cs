namespace ChangeLogMonitor.Core.Models;

/// <summary>
///     Изменение коллекции (навигационное свойство 1-* или *-*)
/// </summary>
public class CollectionChange
{
    public string CollectionName { get; set; } = string.Empty;
    
    public List<CollectionItem> AddedItems { get; set; } = new();

    public List<CollectionItem> RemovedItems { get; set; } = new();
    
    public int AddedCount { get; set; }

    public int RemovedCount { get; set; }

    public bool IsCollapsedToCounters { get; set; }
    
    public string? ViewMessage { get; set; }
}