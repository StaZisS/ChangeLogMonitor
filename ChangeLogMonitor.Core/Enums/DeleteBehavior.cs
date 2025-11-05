namespace ChangeLogMonitor.Core.Enums;

/// <summary>
/// Поведение при удалении сущности
/// </summary>
public enum DeleteBehavior
{
    /// <summary>
    /// Только событие удаления
    /// </summary>
    EventOnly,

    /// <summary>
    /// Событие + все поля удаленной сущности
    /// </summary>
    AllFields
}
