namespace ChangeLogMonitor.Core.Interfaces;

/// <summary>
///     Провайдер человекочитаемых меток для enum значений.
///     Позволяет переопределить логику получения лейблов (например, для локализации).
/// </summary>
/// <remarks>
///     По умолчанию используется <see cref="Attributes.AuditEnumLabelAttribute" />.
///     Реализуйте этот интерфейс для:
///     <list type="bullet">
///         <item>Локализации лейблов (разные языки)</item>
///         <item>Динамических лейблов из базы данных</item>
///         <item>Переопределения стандартных лейблов</item>
///     </list>
/// </remarks>
public interface IEnumLabelProvider
{
    string? GetLabel(Type enumType, object value);

    IReadOnlyDictionary<string, string> GetAllLabels(Type enumType);
}