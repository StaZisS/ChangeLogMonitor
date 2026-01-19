using ChangeLogMonitor.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ChangeLogMonitor.Interceptor.Services;

/// <summary>
///     Результат извлечения метаданных enum.
/// </summary>
public sealed class EnumExtractionResult
{
    /// <summary>
    ///     Снепшоты значений enum: EnumType → (code → label).
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Snapshots { get; } = new();

    /// <summary>
    ///     Маппинг field → enumType для автоопределения без YAML-конфигурации.
    ///     Ключ: (EntityType, FieldName), Значение: EnumTypeName.
    /// </summary>
    public Dictionary<(string EntityType, string FieldName), string> FieldEnumMappings { get; } = new();
}

/// <summary>
///     Сервис для извлечения метаданных enum из сущностей EF Core.
/// </summary>
public sealed class EnumMetadataExtractor
{
    private readonly IEnumLabelProvider _labelProvider;

    public EnumMetadataExtractor(IEnumLabelProvider labelProvider)
    {
        _labelProvider = labelProvider ?? throw new ArgumentNullException(nameof(labelProvider));
    }
    
    public EnumExtractionResult Extract(IEnumerable<EntityEntry> entries)
    {
        var result = new EnumExtractionResult();
        var processedEnums = new HashSet<(Type enumType, long value)>();

        foreach (var entry in entries) ExtractFromEntry(entry, result, processedEnums);

        return result;
    }
    
    public Dictionary<string, Dictionary<string, string>> ExtractEnumSnapshots(IEnumerable<EntityEntry> entries)
    {
        return Extract(entries).Snapshots;
    }

    private void ExtractFromEntry(
        EntityEntry entry,
        EnumExtractionResult result,
        HashSet<(Type enumType, long value)> processedEnums)
    {
        var entityClrType = entry.Entity.GetType();
        var entityTypeName = entry.Metadata.GetTableName() ?? entityClrType.Name;

        foreach (var property in entry.Properties)
        {
            var propertyType = property.Metadata.ClrType;
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (!underlyingType.IsEnum)
                continue;

            var fieldName = property.Metadata.GetColumnName() ?? property.Metadata.Name;
            var enumTypeName = underlyingType.Name;
            
            var mappingKey = (entityTypeName, fieldName);
            result.FieldEnumMappings.TryAdd(mappingKey, enumTypeName);

            CollectEnumValue(underlyingType, property.CurrentValue, result.Snapshots, processedEnums);

            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                CollectEnumValue(underlyingType, property.OriginalValue, result.Snapshots, processedEnums);
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