namespace ChangeLogMonitor.Core.Attributes;

/// <summary>
///     Атрибут для указания человекочитаемой метки значения enum.
///     Используется для сохранения лейблов enum в метаданных аудита.
/// </summary>
/// <example>
///     <code>
/// public enum OrderStatus
/// {
///     [AuditEnumLabel("Новый")]
///     New = 0,
///
///     [AuditEnumLabel("В обработке")]
///     Processing = 1,
///
///     [AuditEnumLabel("Завершён")]
///     Completed = 2
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class AuditEnumLabelAttribute : Attribute
{
    /// <summary>
    ///     Человекочитаемая метка значения enum
    /// </summary>
    public string Label { get; }

    /// <summary>
    ///     Создает атрибут с указанной меткой
    /// </summary>
    /// <param name="label">Человекочитаемая метка для отображения в UI</param>
    public AuditEnumLabelAttribute(string label)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
    }
}
