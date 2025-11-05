namespace ChangeLogMonitor.Core.Enums;

/// <summary>
/// Поведение при создании сущности
/// </summary>
public enum CreateBehavior
{
    /// <summary>
    /// Только событие создания без полей
    /// </summary>
    EventOnly,

    /// <summary>
    /// Событие + все поля сущности
    /// </summary>
    AllFields,

    /// <summary>
    /// Событие + только не-дефолтные поля
    /// </summary>
    NonDefaultFields
}
