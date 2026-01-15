using ChangeLogMonitor.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ChangeLogMonitor.Interceptor.Services;

/// <summary>
///     Сервис для извлечения метаданных enum из сущностей EF Core.
///     Собирает только те enum значения, которые реально встретились в транзакции.
/// </summary>
public sealed class EnumMetadataExtractor
{
    private readonly IEnumLabelProvider _labelProvider;

    public EnumMetadataExtractor(IEnumLabelProvider labelProvider)
    {
        _labelProvider = labelProvider ?? throw new ArgumentNullException(nameof(labelProvider));
    }

    /// <summary>
    ///     Извлекает снепшоты enum из списка изменённых сущностей
    /// </summary>
    /// <param name="entries">Записи ChangeTracker с изменёнными сущностями</param>
    /// <returns>
    ///     Словарь: имя типа enum → (код → лейбл).
    ///     Содержит только значения, реально встретившиеся в транзакции.
    /// </returns>
    public Dictionary<string, Dictionary<string, string>> ExtractEnumSnapshots(IEnumerable<EntityEntry> entries)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        var processedEnums = new HashSet<(Type enumType, long value)>();

        foreach (var entry in entries)
        {
            ExtractFromEntry(entry, result, processedEnums);
        }

        return result;
    }

    private void ExtractFromEntry(
        EntityEntry entry,
        Dictionary<string, Dictionary<string, string>> result,
        HashSet<(Type enumType, long value)> processedEnums)
    {
        var entityType = entry.Entity.GetType();

        foreach (var property in entry.Properties)
        {
            var propertyType = property.Metadata.ClrType;
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (!underlyingType.IsEnum)
                continue;

            // Собираем значения из CurrentValue и OriginalValue
            CollectEnumValue(underlyingType, property.CurrentValue, result, processedEnums);

            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                CollectEnumValue(underlyingType, property.OriginalValue, result, processedEnums);
            }
        }
    }

    private void CollectEnumValue(
        Type enumType,
        object? value,
        Dictionary<string, Dictionary<string, string>> result,
        HashSet<(Type enumType, long value)> processedEnums)
    {
        if (value == null)
            return;

        var numericValue = Convert.ToInt64(value);
        var key = (enumType, numericValue);

        // Пропускаем уже обработанные значения
        if (!processedEnums.Add(key))
            return;

        var enumTypeName = enumType.Name;

        if (!result.TryGetValue(enumTypeName, out var pairs))
        {
            pairs = new Dictionary<string, string>();
            result[enumTypeName] = pairs;
        }

        var code = numericValue.ToString();
        var label = _labelProvider.GetLabel(enumType, value) ?? value.ToString() ?? code;

        pairs[code] = label;
    }
}
