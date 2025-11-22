namespace ChangeLogMonitor.Core.Models;

/// <summary>
///     Изменение коллекции (навигационное свойство 1-* или *-*)
/// </summary>
public class CollectionChange
{
    public string CollectionName { get; set; } = string.Empty;

    // Дельта изменений
    public List<CollectionItem> AddedItems { get; set; } = new();

    public List<CollectionItem> RemovedItems { get; set; } = new();

    // Счетчики (для больших коллекций)
    public int AddedCount { get; set; }

    public int RemovedCount { get; set; }

    public bool IsCollapsedToCounters { get; set; }

    // Финальное сообщение для UI
    public string? ViewMessage { get; set; }
}