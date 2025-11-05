namespace ChangeLogMonitor.Core.Enums;

/// <summary>
/// Действие для поля в политике аудита
/// </summary>
public enum FieldAction
{
    /// <summary>
    /// Включить в аудит без трансформаций
    /// </summary>
    Include,

    /// <summary>
    /// Исключить из аудита полностью
    /// </summary>
    Exclude,

    /// <summary>
    /// Маскировать значение
    /// </summary>
    Mask,

    /// <summary>
    /// Хешировать значение
    /// </summary>
    Hash,

    /// <summary>
    /// Шифровать значение
    /// </summary>
    Encrypt
}
