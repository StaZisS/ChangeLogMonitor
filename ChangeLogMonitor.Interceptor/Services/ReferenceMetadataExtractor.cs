using ChangeLogMonitor.Configuration.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ChangeLogMonitor.Interceptor.Services;

/// <summary>
///     Данные о ссылочном поле (FK) с денормализованным именем
/// </summary>
public record ReferenceSnapshotData(
    string EntityType,
    string FieldName,
    string RelatedEntityType,
    string Key,
    string Title);

/// <summary>
///     Сервис для извлечения информации о ссылочных связях (FK) из сущностей EF Core.
///     Денормализует имена связанных сущностей на момент изменения.
/// </summary>
public sealed class ReferenceMetadataExtractor
{
    private readonly IAuditConfigurationService? _configService;

    public ReferenceMetadataExtractor(IAuditConfigurationService? configService = null)
    {
        _configService = configService;
    }

    /// <summary>
    ///     Извлекает снепшоты ссылочных полей из изменённых сущностей
    /// </summary>
    /// <param name="context">DbContext для доступа к связанным данным</param>
    /// <param name="entries">Записи ChangeTracker с изменёнными сущностями</param>
    /// <returns>Список снепшотов ссылочных полей</returns>
    public List<ReferenceSnapshotData> ExtractReferenceSnapshots(DbContext context, IReadOnlyList<EntityEntry> entries)
    {
        var result = new List<ReferenceSnapshotData>();
        var processedKeys = new HashSet<(string entityType, string fieldName, string key)>();

        foreach (var entry in entries)
        {
            ExtractFromEntry(context, entry, result, processedKeys);
        }

        return result;
    }

    private void ExtractFromEntry(
        DbContext context,
        EntityEntry entry,
        List<ReferenceSnapshotData> result,
        HashSet<(string entityType, string fieldName, string key)> processedKeys)
    {
        var entityType = entry.Metadata;
        var entityTypeName = entityType.ClrType.Name;

        // Находим все FK свойства
        foreach (var fk in entityType.GetForeignKeys())
        {
            var navigation = fk.DependentToPrincipal;
            if (navigation == null)
                continue;

            var fkProperties = fk.Properties;
            if (fkProperties.Count != 1)
                continue; // Поддерживаем только простые FK (один столбец)

            var fkProperty = fkProperties[0];
            var fkPropertyEntry = entry.Property(fkProperty.Name);

            // Получаем текущее и оригинальное значение FK
            var currentValue = fkPropertyEntry.CurrentValue;
            var originalValue = entry.State == EntityState.Modified || entry.State == EntityState.Deleted
                ? fkPropertyEntry.OriginalValue
                : null;

            var relatedEntityType = fk.PrincipalEntityType.ClrType.Name;

            // Обрабатываем текущее значение
            if (currentValue != null)
            {
                var key = currentValue.ToString() ?? string.Empty;
                var cacheKey = (entityTypeName, fkProperty.Name, key);

                if (processedKeys.Add(cacheKey))
                {
                    var title = ResolveTitle(context, fk.PrincipalEntityType, currentValue, entityTypeName, navigation.Name);
                    result.Add(new ReferenceSnapshotData(
                        entityTypeName,
                        fkProperty.Name,
                        relatedEntityType,
                        key,
                        title));
                }
            }

            // Обрабатываем оригинальное значение (если отличается)
            if (originalValue != null && !Equals(originalValue, currentValue))
            {
                var key = originalValue.ToString() ?? string.Empty;
                var cacheKey = (entityTypeName, fkProperty.Name, key);

                if (processedKeys.Add(cacheKey))
                {
                    var title = ResolveTitle(context, fk.PrincipalEntityType, originalValue, entityTypeName, navigation.Name);
                    result.Add(new ReferenceSnapshotData(
                        entityTypeName,
                        fkProperty.Name,
                        relatedEntityType,
                        key,
                        title));
                }
            }
        }
    }

    /// <summary>
    ///     Разрешает человекочитаемое имя связанной сущности
    /// </summary>
    private string ResolveTitle(
        DbContext context,
        IEntityType relatedEntityType,
        object keyValue,
        string ownerEntityType,
        string navigationName)
    {
        var keyString = keyValue.ToString() ?? string.Empty;

        try
        {
            // Пытаемся найти связанную сущность в контексте
            var relatedEntity = context.Find(relatedEntityType.ClrType, keyValue);
            if (relatedEntity == null)
                return keyString; // Fallback на ключ

            // Определяем какое свойство использовать как "имя"
            var namePropertyName = GetNamePropertyName(ownerEntityType, navigationName, relatedEntityType.ClrType);
            if (string.IsNullOrEmpty(namePropertyName))
                return keyString;

            // Получаем значение свойства
            var nameProperty = relatedEntityType.ClrType.GetProperty(namePropertyName);
            if (nameProperty == null)
                return keyString;

            var nameValue = nameProperty.GetValue(relatedEntity);
            return nameValue?.ToString() ?? keyString;
        }
        catch
        {
            return keyString; // При любой ошибке возвращаем ключ
        }
    }

    /// <summary>
    ///     Определяет имя свойства для использования как "имя" связанной сущности
    /// </summary>
    private string? GetNamePropertyName(string ownerEntityType, string navigationName, Type relatedType)
    {
        // Сначала проверяем конфигурацию
        var entityPolicy = _configService?.GetEntityPolicy(ownerEntityType);
        if (entityPolicy?.References.TryGetValue(navigationName, out var refPolicy) == true &&
            !string.IsNullOrEmpty(refPolicy.NameSelector))
        {
            // NameSelector может быть "User.Username" или просто "Username"
            var selector = refPolicy.NameSelector;
            var lastDot = selector.LastIndexOf('.');
            return lastDot >= 0 ? selector[(lastDot + 1)..] : selector;
        }

        // Fallback: пытаемся найти стандартные свойства
        var standardNames = new[] { "Name", "Title", "Username", "FullName", "DisplayName", "Description" };
        foreach (var name in standardNames)
        {
            if (relatedType.GetProperty(name) != null)
                return name;
        }

        return null;
    }
}
