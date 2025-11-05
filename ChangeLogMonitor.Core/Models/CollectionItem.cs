namespace ChangeLogMonitor.Core.Models;

/// <summary>
/// Элемент коллекции в дельте (добавлен/удален)
/// </summary>
public class CollectionItem
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ViewValue { get; set; } = string.Empty;
}
