using ChangeLogMonitor.Core.Services;

namespace ChangeLogMonitor.Core.Extensions;

/// <summary>
///     Extension методы для получения лейблов enum из атрибутов <see cref="Attributes.AuditEnumLabelAttribute"/>.
/// </summary>
public static class EnumLabelExtensions
{
    private static readonly AttributeEnumLabelProvider Provider = new();

    /// <summary>
    ///     Возвращает человекочитаемый лейбл для значения enum.
    ///     Если атрибут <see cref="Attributes.AuditEnumLabelAttribute"/> не найден, возвращает имя значения.
    /// </summary>
    public static string GetLabel<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        return Provider.GetLabel(value);
    }

    /// <summary>
    ///     Создаёт snapshot enum лейблов для указанных значений.
    ///     Используется для передачи в метаданные аудита.
    /// </summary>
    public static Dictionary<string, Dictionary<string, string>> CreateEnumSnapshot<TEnum>(params TEnum[] values)
        where TEnum : struct, Enum
    {
        var pairs = new Dictionary<string, string>();
        foreach (var value in values)
        {
            var code = Convert.ToInt64(value).ToString();
            pairs[code] = Provider.GetLabel(value);
        }

        return new Dictionary<string, Dictionary<string, string>>
        {
            [typeof(TEnum).Name] = pairs
        };
    }

    /// <summary>
    ///     Создаёт полный snapshot всех лейблов enum.
    ///     Используется для raw SQL операций, где неизвестны конкретные значения.
    /// </summary>
    public static Dictionary<string, Dictionary<string, string>> CreateFullEnumSnapshot<TEnum>()
        where TEnum : struct, Enum
    {
        var labels = Provider.GetAllLabels<TEnum>();

        return new Dictionary<string, Dictionary<string, string>>
        {
            [typeof(TEnum).Name] = new Dictionary<string, string>(labels)
        };
    }
}
