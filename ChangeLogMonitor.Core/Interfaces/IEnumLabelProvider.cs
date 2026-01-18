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
    /// <summary>
    ///     Получает человекочитаемую метку для значения enum
    /// </summary>
    /// <param name="enumType">Тип enum</param>
    /// <param name="value">Значение enum</param>
    /// <returns>
    ///     Человекочитаемая метка или null, если метка не найдена.
    ///     При null будет использовано имя значения enum (ToString).
    /// </returns>
    string? GetLabel(Type enumType, object value);

    /// <summary>
    ///     Получает все метки для типа enum
    /// </summary>
    /// <param name="enumType">Тип enum</param>
    /// <returns>Словарь: значение enum (как строка) → человекочитаемая метка</returns>
    IReadOnlyDictionary<string, string> GetAllLabels(Type enumType);
}