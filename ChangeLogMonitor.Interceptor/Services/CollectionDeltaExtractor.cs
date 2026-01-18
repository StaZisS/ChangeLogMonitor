using ChangeLogMonitor.Configuration.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ChangeLogMonitor.Interceptor.Services;

public record CollectionItemData(string Key, string Title);

public record CollectionDeltaData(
    string EntityType,
    string EntityId,
    string FieldName,
    string RelatedEntityType,
    List<CollectionItemData> Added,
    List<CollectionItemData> Removed);

public sealed class CollectionDeltaExtractor
{
    private readonly IAuditConfigurationService? _configService;

    public CollectionDeltaExtractor(IAuditConfigurationService? configService = null)
    {
        _configService = configService;
    }

    public List<CollectionDeltaData> ExtractCollectionDeltas(DbContext context, IReadOnlyList<EntityEntry> entries)
    {
        var result = new List<CollectionDeltaData>();

        var joinTableChanges = new Dictionary<(string ownerType, string ownerId, string collectionName),
            (List<CollectionItemData> added, List<CollectionItemData> removed, string relatedType)>();

        foreach (var entry in entries)
        {
            var entityType = entry.Metadata;
            var m2mInfo = DetectM2MJoinTable(entityType);

            if (m2mInfo != null) ProcessJoinTableEntry(context, entry, m2mInfo.Value, joinTableChanges);
        }

        foreach (var ((ownerType, ownerId, collectionName), (added, removed, relatedType)) in joinTableChanges)
            if (added.Count > 0 || removed.Count > 0)
                result.Add(new CollectionDeltaData(
                    ownerType,
                    ownerId,
                    collectionName,
                    relatedType,
                    added,
                    removed));

        return result;
    }

    private (string ownerType, string ownerFkName, string relatedType, string relatedFkName, string collectionName)?
        DetectM2MJoinTable(IEntityType entityType)
    {
        var fks = entityType.GetForeignKeys().ToList();

        if (fks.Count != 2)
            return null;

        var pk = entityType.FindPrimaryKey();
        if (pk == null)
            return null;

        var pkProperties = pk.Properties.Select(p => p.Name).ToHashSet();
        var fkProperties = fks.SelectMany(fk => fk.Properties.Select(p => p.Name)).ToHashSet();

        if (!pkProperties.SetEquals(fkProperties))
            return null;

        var fk1 = fks[0];
        var fk2 = fks[1];

        var ownerType = fk1.PrincipalEntityType.GetTableName() ?? fk1.PrincipalEntityType.ClrType.Name;
        var ownerFkName = fk1.Properties[0].Name;
        var relatedType = fk2.PrincipalEntityType.GetTableName() ?? fk2.PrincipalEntityType.ClrType.Name;
        var relatedFkName = fk2.Properties[0].Name;

        var collectionName = entityType.GetTableName() ?? entityType.ClrType.Name;

        return (ownerType, ownerFkName, relatedType, relatedFkName, collectionName);
    }

    private void ProcessJoinTableEntry(
        DbContext context,
        EntityEntry entry,
        (string ownerType, string ownerFkName, string relatedType, string relatedFkName, string collectionName) m2mInfo,
        Dictionary<(string, string, string), (List<CollectionItemData>, List<CollectionItemData>, string)> changes)
    {
        var ownerIdValue = entry.Property(m2mInfo.ownerFkName).CurrentValue?.ToString();
        var relatedIdValue = entry.Property(m2mInfo.relatedFkName).CurrentValue?.ToString();

        if (string.IsNullOrEmpty(ownerIdValue) || string.IsNullOrEmpty(relatedIdValue))
            return;

        var key = (m2mInfo.ownerType, ownerIdValue, m2mInfo.collectionName);

        if (!changes.TryGetValue(key, out var lists))
        {
            lists = (new List<CollectionItemData>(), new List<CollectionItemData>(), m2mInfo.relatedType);
            changes[key] = lists;
        }

        var title = ResolveRelatedItemTitle(context, m2mInfo.relatedType, relatedIdValue, m2mInfo.ownerType,
            m2mInfo.collectionName);
        var item = new CollectionItemData(relatedIdValue, title);

        switch (entry.State)
        {
            case EntityState.Added:
                lists.Item1.Add(item);
                break;
            case EntityState.Deleted:
                lists.Item2.Add(item);
                break;
        }
    }

    private string ResolveRelatedItemTitle(
        DbContext context,
        string relatedTypeName,
        string relatedId,
        string ownerTypeName,
        string collectionName)
    {
        try
        {
            var relatedEntityType = context.Model.FindEntityType(relatedTypeName)
                                    ?? context.Model.GetEntityTypes()
                                        .FirstOrDefault(e => e.ClrType.Name == relatedTypeName);

            if (relatedEntityType == null)
                return relatedId;

            var keyProperty = relatedEntityType.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (keyProperty == null)
                return relatedId;

            var keyValue = ConvertToKeyType(relatedId, keyProperty.ClrType);
            if (keyValue == null)
                return relatedId;

            var entity = context.Find(relatedEntityType.ClrType, keyValue);
            if (entity == null)
                return relatedId;

            var namePropertyName = GetNamePropertyName(ownerTypeName, collectionName, relatedEntityType.ClrType);
            if (string.IsNullOrEmpty(namePropertyName))
                return relatedId;

            var nameProperty = relatedEntityType.ClrType.GetProperty(namePropertyName);
            var nameValue = nameProperty?.GetValue(entity);

            return nameValue?.ToString() ?? relatedId;
        }
        catch
        {
            return relatedId;
        }
    }

    private static object? ConvertToKeyType(string keyString, Type keyType)
    {
        try
        {
            if (keyType == typeof(Guid))
                return Guid.Parse(keyString);
            if (keyType == typeof(int))
                return int.Parse(keyString);
            if (keyType == typeof(long))
                return long.Parse(keyString);
            if (keyType == typeof(string))
                return keyString;

            return Convert.ChangeType(keyString, keyType);
        }
        catch
        {
            return null;
        }
    }

    private string? GetNamePropertyName(string ownerEntityType, string collectionName, Type relatedType)
    {
        var entityPolicy = _configService?.GetEntityPolicy(ownerEntityType);
        if (entityPolicy?.Collections.TryGetValue(collectionName, out var collPolicy) == true &&
            !string.IsNullOrEmpty(collPolicy.ItemNameSelector))
        {
            var selector = collPolicy.ItemNameSelector;
            var lastDot = selector.LastIndexOf('.');
            return lastDot >= 0 ? selector[(lastDot + 1)..] : selector;
        }

        var standardNames = new[] { "Name", "Title", "DisplayName", "Description" };
        foreach (var name in standardNames)
            if (relatedType.GetProperty(name) != null)
                return name;

        return null;
    }
}