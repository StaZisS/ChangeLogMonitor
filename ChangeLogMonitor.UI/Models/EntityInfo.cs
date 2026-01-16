namespace ChangeLogMonitor.UI.Models;

/// <summary>
///     Информация о сущности для отображения в UI
/// </summary>
public class EntityInfo
{
    /// <summary>
    ///     Имя сущности (table/collection name)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Отображаемое имя для UI
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
