using ChangeLogMonitor.Configuration.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ChangeLogMonitor.Interceptor.Services;

public record ReferenceSnapshotData(
    string EntityType,
    string FieldName,
    string RelatedEntityType,
    string Key,
    string Title);

/// <summary>
///     Сервис для извлечения информации о ссылочных связях (FK) из сущностей EF Core.
/// </summary>
public sealed class ReferenceMetadataExtractor
{
    private readonly IAuditConfigurationService? _configService;

    public ReferenceMetadataExtractor(IAuditConfigurationService? configService = null)
    {
        _configService = configService;
    }
    
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
        
        foreach (var fk in entityType.GetForeignKeys())
        {
            var navigation = fk.DependentToPrincipal;
            if (navigation == null)
                continue;

            var fkProperties = fk.Properties;
            if (fkProperties.Count != 1)
                continue;

            var fkProperty = fkProperties[0];
            var fkPropertyEntry = entry.Property(fkProperty.Name);
            
            var currentValue = fkPropertyEntry.CurrentValue;
            var originalValue = entry.State == EntityState.Modified || entry.State == EntityState.Deleted
                ? fkPropertyEntry.OriginalValue
                : null;

            var relatedEntityType = fk.PrincipalEntityType.ClrType.Name;
            
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
            var relatedEntity = context.Find(relatedEntityType.ClrType, keyValue);
            if (relatedEntity == null)
                return keyString;
            
            var namePropertyName = GetNamePropertyName(ownerEntityType, navigationName, relatedEntityType.ClrType);
            if (string.IsNullOrEmpty(namePropertyName))
                return keyString;
            
            var nameProperty = relatedEntityType.ClrType.GetProperty(namePropertyName);
            if (nameProperty == null)
                return keyString;

            var nameValue = nameProperty.GetValue(relatedEntity);
            return nameValue?.ToString() ?? keyString;
        }
        catch
        {
            return keyString;
        }
    }
    
    private string? GetNamePropertyName(string ownerEntityType, string navigationName, Type relatedType)
    {
        var entityPolicy = _configService?.GetEntityPolicy(ownerEntityType);
        if (entityPolicy?.References.TryGetValue(navigationName, out var refPolicy) == true &&
            !string.IsNullOrEmpty(refPolicy.NameSelector))
        {
            var selector = refPolicy.NameSelector;
            var lastDot = selector.LastIndexOf('.');
            return lastDot >= 0 ? selector[(lastDot + 1)..] : selector;
        }
        
        var standardNames = new[] { "Name", "Title", "Username", "FullName", "DisplayName", "Description" };
        foreach (var name in standardNames)
        {
            if (relatedType.GetProperty(name) != null)
                return name;
        }

        return null;
    }
}
