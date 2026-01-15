using System.Collections.Concurrent;
using System.Reflection;
using ChangeLogMonitor.Core.Attributes;
using ChangeLogMonitor.Core.Interfaces;

namespace ChangeLogMonitor.Core.Services;

/// <summary>
///     Провайдер лейблов enum на основе атрибутов <see cref="AuditEnumLabelAttribute" />.
///     Это реализация по умолчанию для <see cref="IEnumLabelProvider" />.
/// </summary>
public sealed class AttributeEnumLabelProvider : IEnumLabelProvider
{
    private readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, string>> _cache = new();

    /// <inheritdoc />
    public string? GetLabel(Type enumType, object value)
    {
        if (enumType == null) throw new ArgumentNullException(nameof(enumType));
        if (value == null) throw new ArgumentNullException(nameof(value));

        if (!enumType.IsEnum)
            throw new ArgumentException($"Type {enumType.Name} is not an enum", nameof(enumType));

        var labels = GetAllLabels(enumType);
        var key = Convert.ToInt64(value).ToString();

        return labels.TryGetValue(key, out var label) ? label : null;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetAllLabels(Type enumType)
    {
        if (enumType == null) throw new ArgumentNullException(nameof(enumType));

        if (!enumType.IsEnum)
            throw new ArgumentException($"Type {enumType.Name} is not an enum", nameof(enumType));

        return _cache.GetOrAdd(enumType, BuildLabelsDictionary);
    }

    private static IReadOnlyDictionary<string, string> BuildLabelsDictionary(Type enumType)
    {
        var result = new Dictionary<string, string>();

        foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = field.GetValue(null);
            if (value == null) continue;

            var code = Convert.ToInt64(value).ToString();

            // Пытаемся получить лейбл из атрибута
            var attribute = field.GetCustomAttribute<AuditEnumLabelAttribute>();
            var label = attribute?.Label ?? field.Name;

            result[code] = label;
        }

        return result;
    }
}
