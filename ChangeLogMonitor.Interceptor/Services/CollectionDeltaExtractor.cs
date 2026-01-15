using ChangeLogMonitor.Configuration.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ChangeLogMonitor.Interceptor.Services;

/// <summary>
///     Элемент коллекции с денормализованным именем
/// </summary>
public record CollectionItemData(string Key, string Title);

/// <summary>
///     Дельта изменений коллекции
/// </summary>
public record CollectionDeltaData(
    string EntityType,
    string EntityId,
    string FieldName,
    string RelatedEntityType,
    List<CollectionItemData> Added,
    List<CollectionItemData> Removed);

/// <summary>
///     Сервис для извлечения дельт коллекций из изменений EF Core.
///     Отслеживает добавленные и удалённые элементы в связях M2M.
/// </summary>
public sealed class CollectionDeltaExtractor
{
    private readonly IAuditConfigurationService? _configService;

    public CollectionDeltaExtractor(IAuditConfigurationService? configService = null)
    {
        _configService = configService;
    }

    /// <summary>
    ///     Извлекает дельты коллекций из изменённых сущностей
    /// </summary>
    /// <param name="context">DbContext для доступа к связанным данным</param>
    /// <param name="entries">Записи ChangeTracker с изменёнными сущностями</param>
    /// <returns>Список дельт коллекций</returns>
    public List<CollectionDeltaData> ExtractCollectionDeltas(DbContext context, IReadOnlyList<EntityEntry> entries)
    {
        var result = new List<CollectionDeltaData>();

        // Группируем изменения join-таблиц по владельцу коллекции
        var joinTableChanges = new Dictionary<(string ownerType, string ownerId, string collectionName),
            (List<CollectionItemData> added, List<CollectionItemData> removed, string relatedType)>();

        foreach (var entry in entries)
        {
            // Проверяем, является ли это join-таблицей (M2M)
            var entityType = entry.Metadata;
            var m2mInfo = DetectM2MJoinTable(entityType);

            if (m2mInfo != null)
            {
                ProcessJoinTableEntry(context, entry, m2mInfo.Value, joinTableChanges);
            }
        }

        // Конвертируем в результат
        foreach (var ((ownerType, ownerId, collectionName), (added, removed, relatedType)) in joinTableChanges)
        {
            if (added.Count > 0 || removed.Count > 0)
            {
                result.Add(new CollectionDeltaData(
                    ownerType,
                    ownerId,
                    collectionName,
                    relatedType,
                    added,
                    removed));
            }
        }

        return result;
    }

    /// <summary>
    ///     Определяет, является ли сущность join-таблицей для M2M связи
    /// </summary>
    private (string ownerType, string ownerFkName, string relatedType, string relatedFkName, string collectionName)?
        DetectM2MJoinTable(IEntityType entityType)
    {
        var fks = entityType.GetForeignKeys().ToList();

        // Join-таблица обычно имеет ровно 2 FK и составной PK из этих FK
        if (fks.Count != 2)
            return null;

        var pk = entityType.FindPrimaryKey();
        if (pk == null)
            return null;

        var pkProperties = pk.Properties.Select(p => p.Name).ToHashSet();
        var fkProperties = fks.SelectMany(fk => fk.Properties.Select(p => p.Name)).ToHashSet();

        // PK должен состоять из FK-свойств
        if (!pkProperties.SetEquals(fkProperties))
            return null;

        // Первый FK - владелец, второй - связанный элемент
        // Определяем на основе конфигурации или соглашения об именовании
        var fk1 = fks[0];
        var fk2 = fks[1];

        // Используем имя таблицы из БД, а не имя класса C#
        var ownerType = fk1.PrincipalEntityType.GetTableName() ?? fk1.PrincipalEntityType.ClrType.Name;
        var ownerFkName = fk1.Properties[0].Name;
        var relatedType = fk2.PrincipalEntityType.GetTableName() ?? fk2.PrincipalEntityType.ClrType.Name;
        var relatedFkName = fk2.Properties[0].Name;

        // Имя коллекции - имя join-таблицы
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

        // Получаем имя связанного элемента
        var title = ResolveRelatedItemTitle(context, m2mInfo.relatedType, relatedIdValue, m2mInfo.ownerType, m2mInfo.collectionName);
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

    /// <summary>
    ///     Разрешает человекочитаемое имя элемента коллекции
    /// </summary>
    private string ResolveRelatedItemTitle(
        DbContext context,
        string relatedTypeName,
        string relatedId,
        string ownerTypeName,
        string collectionName)
    {
        try
        {
            // Находим тип сущности
            var relatedEntityType = context.Model.FindEntityType(relatedTypeName)
                ?? context.Model.GetEntityTypes().FirstOrDefault(e => e.ClrType.Name == relatedTypeName);

            if (relatedEntityType == null)
                return relatedId;

            // Пытаемся найти сущность
            var keyProperty = relatedEntityType.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (keyProperty == null)
                return relatedId;

            var keyValue = ConvertToKeyType(relatedId, keyProperty.ClrType);
            if (keyValue == null)
                return relatedId;

            var entity = context.Find(relatedEntityType.ClrType, keyValue);
            if (entity == null)
                return relatedId;

            // Определяем свойство для имени
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
        // Проверяем конфигурацию
        var entityPolicy = _configService?.GetEntityPolicy(ownerEntityType);
        if (entityPolicy?.Collections.TryGetValue(collectionName, out var collPolicy) == true &&
            !string.IsNullOrEmpty(collPolicy.ItemNameSelector))
        {
            var selector = collPolicy.ItemNameSelector;
            var lastDot = selector.LastIndexOf('.');
            return lastDot >= 0 ? selector[(lastDot + 1)..] : selector;
        }

        // Fallback: стандартные имена
        var standardNames = new[] { "Name", "Title", "DisplayName", "Description" };
        foreach (var name in standardNames)
        {
            if (relatedType.GetProperty(name) != null)
                return name;
        }

        return null;
    }
}
