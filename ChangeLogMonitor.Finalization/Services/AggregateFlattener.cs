using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Audit.V1;
using Auditmeta.Raw;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Core.Enums;
using ChangeLogMonitor.DataAggregator.Models;
using ChangeLogMonitor.Finalization.Models;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace ChangeLogMonitor.Finalization.Services;

public sealed class AggregateFlattener(
    ILogger<AggregateFlattener> logger,
    IAuditConfigurationService? configurationService = null) : IAggregateFlattener
{
    private static readonly JsonSerializerOptions AggregateSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] EntityIdCandidates =
    [
        "id",
        "entity_id",
        "entityid",
        "key",
        "pk"
    ];

    private readonly IAuditConfigurationService? _configurationService = configurationService;

    public IReadOnlyCollection<AuditLogRecord> Flatten(string? rawPayload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return Array.Empty<AuditLogRecord>();
        }

        TxAggregate? aggregate;
        try
        {
            aggregate = JsonSerializer.Deserialize<TxAggregate>(rawPayload, AggregateSerializerOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse aggregate payload: {Payload}", Truncate(rawPayload));
            return Array.Empty<AuditLogRecord>();
        }

        if (aggregate?.Events == null || aggregate.Events.Count == 0 || string.IsNullOrWhiteSpace(aggregate.TxId))
        {
            return Array.Empty<AuditLogRecord>();
        }

        var normalizedMeta = NormalizeMeta(aggregate.Meta);
        var (userId, userName) = ResolveUserInfo(normalizedMeta);
        var enumSnapshots = ExtractEnumSnapshots(normalizedMeta);
        var referenceSnapshots = ExtractReferenceSnapshots(normalizedMeta);
        var collectionDeltas = ExtractCollectionDeltas(normalizedMeta);
        var normalizationVersion = ResolveNormalizationVersion();
        var records = new List<AuditLogRecord>(aggregate.Events.Count);
        
        var processedEntities = new HashSet<(string entityType, string entityId)>();

        for (var i = 0; i < aggregate.Events.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var aggregateEvent = aggregate.Events[i];

            var changeTime = ResolveTimestamp(aggregateEvent.TimestampMs);
            var entityId = ResolveEntityId(aggregateEvent.Payload);
            var operation = OperationCodeExtensions.FromCdcOperation(aggregateEvent.Operation);
            var tableName = aggregateEvent.Table?.Trim() ?? string.Empty;

            processedEntities.Add((tableName, entityId));

            var auditRecord = BuildAuditRecord(
                aggregate,
                aggregateEvent,
                i,
                changeTime,
                entityId,
                userId,
                operation,
                normalizationVersion,
                enumSnapshots,
                referenceSnapshots,
                collectionDeltas);

            var payload = Convert.ToBase64String(auditRecord.ToByteArray());

            records.Add(new AuditLogRecord(
                0,
                changeTime,
                userId,
                userName,
                tableName,
                operation,
                entityId,
                aggregate.TxId,
                payload));
        }
        
        var syntheticRecords = BuildSyntheticCollectionRecords(
            aggregate,
            collectionDeltas,
            processedEntities,
            userId,
            userName,
            normalizationVersion);

        records.AddRange(syntheticRecords);

        return records;
    }

    private AuditRecord BuildAuditRecord(
        TxAggregate aggregate,
        TxAggregateEvent aggregateEvent,
        int ordinal,
        DateTime changeTime,
        string entityId,
        string userId,
        OperationCode operation,
        uint normalizationVersion,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> enumSnapshots,
        IReadOnlyDictionary<(string entityType, string fieldName), ReferenceSnapshotInfo> referenceSnapshots,
        IReadOnlyList<CollectionDeltaInfo> collectionDeltas)
    {
        var tableName = aggregateEvent.Table ?? string.Empty;
        var record = new AuditRecord
        {
            Id = $"{aggregate.TxId}-{ordinal}",
            EntityType = tableName,
            EntityId = entityId,
            EntityTitle = string.Empty,
            Operation = (OperationType)(byte)operation,
            TimestampUtc = Timestamp.FromDateTime(DateTime.SpecifyKind(changeTime, DateTimeKind.Utc)),
            UserId = userId,
            UserTitle = string.Empty,
            UserType = string.IsNullOrWhiteSpace(userId) ? "system" : "user",
            RawPayloadJson = aggregateEvent.Payload.GetRawText(),
            NormalizationVersion = normalizationVersion
        };
        
        var fieldChanges = BuildFieldChanges(tableName, operation, aggregateEvent.Payload, enumSnapshots, referenceSnapshots);
        foreach (var fc in fieldChanges)
        {
            record.FieldChanges.Add(fc);
        }
        
        var entityCollectionDeltas = collectionDeltas
            .Where(d => d.EntityType.Equals(tableName, StringComparison.OrdinalIgnoreCase) &&
                        d.EntityId == entityId)
            .ToList();

        foreach (var delta in entityCollectionDeltas)
        {
            var collectionChange = new Audit.V1.CollectionChange
            {
                FieldName = delta.FieldName,
                FieldTitle = delta.FieldName
            };

            foreach (var added in delta.Added)
            {
                collectionChange.Items.Add(new Audit.V1.CollectionItemChange
                {
                    Kind = Audit.V1.CollectionDeltaKind.Add,
                    ItemKey = added.Key,
                    ItemTitle = added.Title,
                    RawNormalized = added.Title
                });
            }

            foreach (var removed in delta.Removed)
            {
                collectionChange.Items.Add(new Audit.V1.CollectionItemChange
                {
                    Kind = Audit.V1.CollectionDeltaKind.Remove,
                    ItemKey = removed.Key,
                    ItemTitle = removed.Title,
                    RawNormalized = removed.Title
                });
            }

            if (collectionChange.Items.Count > 0)
            {
                record.CollectionChanges.Add(collectionChange);
            }
        }

        return record;
    }
    
    private List<AuditLogRecord> BuildSyntheticCollectionRecords(
        TxAggregate aggregate,
        IReadOnlyList<CollectionDeltaInfo> collectionDeltas,
        HashSet<(string entityType, string entityId)> processedEntities,
        string userId,
        string userName,
        uint normalizationVersion)
    {
        var result = new List<AuditLogRecord>();

        // Group collection deltas by owner entity
        var deltasByOwner = collectionDeltas
            .GroupBy(d => (d.EntityType, d.EntityId))
            .Where(g => !processedEntities.Contains(g.Key)) // Only for entities not already processed
            .ToList();

        var ordinal = aggregate.Events?.Count ?? 0;

        foreach (var group in deltasByOwner)
        {
            var (entityType, entityId) = group.Key;
            var changeTime = DateTime.UtcNow;
            
            var record = new AuditRecord
            {
                Id = $"{aggregate.TxId}-coll-{ordinal}",
                EntityType = entityType,
                EntityId = entityId,
                EntityTitle = string.Empty,
                Operation = OperationType.OperationUpdate, // Collection change is like an update
                TimestampUtc = Timestamp.FromDateTime(DateTime.SpecifyKind(changeTime, DateTimeKind.Utc)),
                UserId = userId,
                UserTitle = string.Empty,
                UserType = string.IsNullOrWhiteSpace(userId) ? "system" : "user",
                RawPayloadJson = "{}",
                NormalizationVersion = normalizationVersion
            };
            
            foreach (var delta in group)
            {
                var collectionChange = new Audit.V1.CollectionChange
                {
                    FieldName = delta.FieldName,
                    FieldTitle = delta.FieldName
                };

                foreach (var added in delta.Added)
                {
                    collectionChange.Items.Add(new Audit.V1.CollectionItemChange
                    {
                        Kind = Audit.V1.CollectionDeltaKind.Add,
                        ItemKey = added.Key,
                        ItemTitle = added.Title,
                        RawNormalized = added.Title
                    });
                }

                foreach (var removed in delta.Removed)
                {
                    collectionChange.Items.Add(new Audit.V1.CollectionItemChange
                    {
                        Kind = Audit.V1.CollectionDeltaKind.Remove,
                        ItemKey = removed.Key,
                        ItemTitle = removed.Title,
                        RawNormalized = removed.Title
                    });
                }

                if (collectionChange.Items.Count > 0)
                {
                    record.CollectionChanges.Add(collectionChange);
                }
            }

            if (record.CollectionChanges.Count > 0)
            {
                var payload = Convert.ToBase64String(record.ToByteArray());

                result.Add(new AuditLogRecord(
                    0,
                    changeTime,
                    userId,
                    userName,
                    entityType,
                    OperationCode.Update,
                    entityId,
                    aggregate.TxId,
                    payload));

                ordinal++;
            }
        }

        return result;
    }

    private IEnumerable<FieldChange> BuildFieldChanges(
        string tableName,
        OperationCode operation,
        JsonElement payload,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> enumSnapshots,
        IReadOnlyDictionary<(string entityType, string fieldName), ReferenceSnapshotInfo> referenceSnapshots)
    {
        var entityPolicy = _configurationService?.GetEntityPolicy(tableName);
        var globalPolicy = _configurationService?.GetPolicy();

        // Determine behavior based on operation
        var shouldIncludeFields = operation switch
        {
            OperationCode.Create => ShouldIncludeFieldsOnCreate(entityPolicy, globalPolicy),
            OperationCode.Update => true, // update always includes delta
            OperationCode.Delete => ShouldIncludeFieldsOnDelete(entityPolicy, globalPolicy),
            _ => false
        };

        if (!shouldIncludeFields)
            yield break;
        
        JsonElement? before = null;
        JsonElement? after = null;

        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (TryGetPropertyCaseInsensitive(payload, "before", out var beforeEl))
                before = beforeEl.ValueKind != JsonValueKind.Null ? beforeEl : null;

            if (TryGetPropertyCaseInsensitive(payload, "after", out var afterEl))
                after = afterEl.ValueKind != JsonValueKind.Null ? afterEl : null;
            
            if (before == null && after == null)
            {
                after = payload;
            }
        }

        // Build field changes based on operation
        if (operation == OperationCode.Create)
        {
            // For create, show all fields from "after" as new values
            if (after?.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in after.Value.EnumerateObject())
                {
                    var fieldChange = ProcessField(prop.Name, null, prop.Value, entityPolicy, globalPolicy, enumSnapshots, referenceSnapshots, tableName);
                    if (fieldChange != null)
                        yield return fieldChange;
                }
            }
        }
        else if (operation == OperationCode.Update)
        {
            if (before?.ValueKind == JsonValueKind.Object && after?.ValueKind == JsonValueKind.Object)
            {
                var beforeProps = before.Value.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                var afterProps = after.Value.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

                foreach (var afterProp in afterProps)
                {
                    var beforeValue = beforeProps.TryGetValue(afterProp.Key, out var bv) ? bv : (JsonElement?)null;

                    var oldStr = beforeValue.HasValue ? ExtractScalar(beforeValue.Value) : string.Empty;
                    var newStr = ExtractScalar(afterProp.Value);
                    if (oldStr == newStr)
                        continue;

                    var fieldChange = ProcessField(afterProp.Key, beforeValue, afterProp.Value, entityPolicy, globalPolicy, enumSnapshots, referenceSnapshots, tableName);
                    if (fieldChange != null)
                        yield return fieldChange;
                }
            }
            else if (after?.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in after.Value.EnumerateObject())
                {
                    var fieldChange = ProcessField(prop.Name, null, prop.Value, entityPolicy, globalPolicy, enumSnapshots, referenceSnapshots, tableName);
                    if (fieldChange != null)
                        yield return fieldChange;
                }
            }
        }
        else if (operation == OperationCode.Delete)
        {
            if (before?.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in before.Value.EnumerateObject())
                {
                    var fieldChange = ProcessField(prop.Name, prop.Value, null, entityPolicy, globalPolicy, enumSnapshots, referenceSnapshots, tableName);
                    if (fieldChange != null)
                        yield return fieldChange;
                }
            }
        }
    }

    private FieldChange? ProcessField(
        string fieldName,
        JsonElement? oldValue,
        JsonElement? newValue,
        ChangeLogMonitor.Core.Models.Policy.EntityPolicy? entityPolicy,
        ChangeLogMonitor.Core.Models.Policy.AuditPolicy? globalPolicy,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> enumSnapshots,
        IReadOnlyDictionary<(string entityType, string fieldName), ReferenceSnapshotInfo> referenceSnapshots,
        string tableName)
    {
        if (IsSystemField(fieldName))
            return null;
        
        if (globalPolicy?.GlobalFieldExclusions?.Contains(fieldName, StringComparer.OrdinalIgnoreCase) == true)
            return null;
        
        var fieldPolicy = entityPolicy?.Fields?.TryGetValue(fieldName, out var fp) == true ? fp : null;
        
        if (fieldPolicy?.Action == ChangeLogMonitor.Core.Enums.FieldAction.Exclude)
            return null;

        var oldStr = oldValue.HasValue ? ExtractScalar(oldValue.Value) : null;
        var newStr = newValue.HasValue ? ExtractScalar(newValue.Value) : null;
        
        if (string.IsNullOrEmpty(oldStr) && string.IsNullOrEmpty(newStr))
            return null;
        
        var sensitiveMode = SensitiveMode.None;
        string? processedOld = oldStr;
        string? processedNew = newStr;

        if (fieldPolicy != null)
        {
            switch (fieldPolicy.Action)
            {
                case ChangeLogMonitor.Core.Enums.FieldAction.Mask:
                    sensitiveMode = SensitiveMode.Masked;
                    processedOld = ApplyMask(oldStr, fieldPolicy.Mask, globalPolicy);
                    processedNew = ApplyMask(newStr, fieldPolicy.Mask, globalPolicy);
                    break;

                case ChangeLogMonitor.Core.Enums.FieldAction.Hash:
                    sensitiveMode = SensitiveMode.Hashed;
                    processedOld = ApplyHash(oldStr, fieldPolicy.Hash, globalPolicy);
                    processedNew = ApplyHash(newStr, fieldPolicy.Hash, globalPolicy);
                    break;

                case ChangeLogMonitor.Core.Enums.FieldAction.Encrypt:
                    sensitiveMode = SensitiveMode.Encrypted;
                    processedOld = oldStr != null ? "[ENCRYPTED]" : null;
                    processedNew = newStr != null ? "[ENCRYPTED]" : null;
                    break;
            }
        }
        
        var valueKind = ValueKind.Scalar;
        var enumType = fieldPolicy?.View?.EnumType;
        IReadOnlyDictionary<string, string>? enumLabels = null;
        ReferenceSnapshotInfo? referenceInfo = null;
        
        var refKey = (tableName, fieldName);
        if (referenceSnapshots.TryGetValue(refKey, out var refSnapshot))
        {
            referenceInfo = refSnapshot;
            valueKind = ValueKind.Reference;
        }
        else if (fieldPolicy?.View?.Format == ChangeLogMonitor.Core.Enums.FieldType.Enum && !string.IsNullOrEmpty(enumType))
        {
            enumSnapshots.TryGetValue(enumType, out enumLabels);
            valueKind = ValueKind.Enum;
        }
        else if (enumSnapshots.Count > 0)
        {
            foreach (var (typeName, labels) in enumSnapshots)
            {
                if (typeName.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                    typeName.EndsWith(fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    enumLabels = labels;
                    valueKind = ValueKind.Enum;
                    break;
                }
            }
        }
        
        FieldValue? oldFieldValue = null;
        FieldValue? newFieldValue = null;

        if (processedOld != null)
        {
            oldFieldValue = new FieldValue { Normalized = processedOld };
            if (valueKind == ValueKind.Enum && enumLabels != null)
            {
                oldFieldValue.EnumCode = oldStr ?? string.Empty;
                oldFieldValue.EnumTitle = enumLabels.TryGetValue(oldStr ?? string.Empty, out var oldLabel)
                    ? oldLabel
                    : processedOld;
            }
            else if (valueKind == ValueKind.Reference && referenceInfo != null)
            {
                oldFieldValue.ReferenceKey = oldStr ?? string.Empty;
                oldFieldValue.ReferenceTitle = referenceInfo.KeyTitles.TryGetValue(oldStr ?? string.Empty, out var oldRefTitle)
                    ? oldRefTitle
                    : oldStr ?? string.Empty;
            }
        }

        if (processedNew != null)
        {
            newFieldValue = new FieldValue { Normalized = processedNew };
            if (valueKind == ValueKind.Enum && enumLabels != null)
            {
                newFieldValue.EnumCode = newStr ?? string.Empty;
                newFieldValue.EnumTitle = enumLabels.TryGetValue(newStr ?? string.Empty, out var newLabel)
                    ? newLabel
                    : processedNew;
            }
            else if (valueKind == ValueKind.Reference && referenceInfo != null)
            {
                newFieldValue.ReferenceKey = newStr ?? string.Empty;
                newFieldValue.ReferenceTitle = referenceInfo.KeyTitles.TryGetValue(newStr ?? string.Empty, out var newRefTitle)
                    ? newRefTitle
                    : newStr ?? string.Empty;
            }
        }

        return new FieldChange
        {
            FieldName = fieldName,
            FieldTitle = fieldName,
            ValueKind = valueKind,
            SensitiveMode = sensitiveMode,
            OldValue = oldFieldValue,
            NewValue = newFieldValue
        };
    }

    private static string? ApplyMask(string? value, ChangeLogMonitor.Core.Models.Policy.MaskSettings? settings, ChangeLogMonitor.Core.Models.Policy.AuditPolicy? globalPolicy)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var maskChar = settings?.MaskChar ?? '*';
        var keepLeft = settings?.KeepLeft ?? 0;
        var keepRight = settings?.KeepRight ?? 0;
        
        if (!string.IsNullOrEmpty(settings?.Preset) &&
            globalPolicy?.MaskPresets?.TryGetValue(settings.Preset, out var preset) == true)
        {
            maskChar = preset.MaskChar;
            keepLeft = preset.KeepLeft;
            keepRight = preset.KeepRight;
        }

        if (value.Length <= keepLeft + keepRight)
            return new string(maskChar, value.Length);

        var left = keepLeft > 0 ? value.Substring(0, keepLeft) : string.Empty;
        var right = keepRight > 0 ? value.Substring(value.Length - keepRight) : string.Empty;
        var middle = new string(maskChar, value.Length - keepLeft - keepRight);

        return left + middle + right;
    }

    private static string? ApplyHash(string? value, ChangeLogMonitor.Core.Models.Policy.HashSettings? settings, ChangeLogMonitor.Core.Models.Policy.AuditPolicy? globalPolicy)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var algo = settings?.Algo ?? "SHA-256";
        
        if (!string.IsNullOrEmpty(settings?.Preset) &&
            globalPolicy?.HashPresets?.TryGetValue(settings.Preset, out var preset) == true)
        {
            algo = preset.Algo;
        }

        try
        {
            System.Security.Cryptography.HashAlgorithm hashAlgorithm = algo.ToUpperInvariant() switch
            {
                "SHA-256" or "SHA256" => System.Security.Cryptography.SHA256.Create(),
                "SHA-384" or "SHA384" => System.Security.Cryptography.SHA384.Create(),
                "SHA-512" or "SHA512" => System.Security.Cryptography.SHA512.Create(),
                "MD5" => System.Security.Cryptography.MD5.Create(),
                _ => System.Security.Cryptography.SHA256.Create()
            };

            using (hashAlgorithm)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(value);
                var hash = hashAlgorithm.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
        catch
        {
            return "[HASH_ERROR]";
        }
    }

    private static bool ShouldIncludeFieldsOnCreate(
        ChangeLogMonitor.Core.Models.Policy.EntityPolicy? entityPolicy,
        ChangeLogMonitor.Core.Models.Policy.AuditPolicy? globalPolicy)
    {
        // Check entity-level policy first
        if (entityPolicy?.OnCreate != null)
        {
            return entityPolicy.OnCreate != ChangeLogMonitor.Core.Enums.CreateBehavior.EventOnly;
        }

        // Fall back to global policy
        if (globalPolicy != null)
        {
            return globalPolicy.OnCreate != ChangeLogMonitor.Core.Enums.CreateBehavior.EventOnly;
        }

        return false;
    }

    private static bool ShouldIncludeFieldsOnDelete(
        ChangeLogMonitor.Core.Models.Policy.EntityPolicy? entityPolicy,
        ChangeLogMonitor.Core.Models.Policy.AuditPolicy? globalPolicy)
    {
        // Check entity-level policy first
        if (entityPolicy?.OnDelete != null)
        {
            return entityPolicy.OnDelete != ChangeLogMonitor.Core.Enums.DeleteBehavior.EventOnly;
        }

        // Fall back to global policy
        if (globalPolicy != null)
        {
            return globalPolicy.OnDelete != ChangeLogMonitor.Core.Enums.DeleteBehavior.EventOnly;
        }

        return false;
    }

    private static bool IsSystemField(string fieldName)
    {
        return fieldName.StartsWith("__", StringComparison.Ordinal) ||
               fieldName.Equals("lsn", StringComparison.OrdinalIgnoreCase) ||
               fieldName.Equals("txId", StringComparison.OrdinalIgnoreCase) ||
               fieldName.Equals("ts_ms", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime ResolveTimestamp(long timestampMs)
    {
        var ts = timestampMs > 0 ? timestampMs : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return DateTimeOffset.FromUnixTimeMilliseconds(ts).UtcDateTime;
    }

    private static string ResolveEntityId(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return string.Empty;

        // Try to get entity ID from "after" first (for create/update), then "before" (for delete)
        JsonElement targetElement = payload;

        if (TryGetPropertyCaseInsensitive(payload, "after", out var afterEl) &&
            afterEl.ValueKind == JsonValueKind.Object)
        {
            targetElement = afterEl;
        }
        else if (TryGetPropertyCaseInsensitive(payload, "before", out var beforeEl) &&
                 beforeEl.ValueKind == JsonValueKind.Object)
        {
            targetElement = beforeEl;
        }

        foreach (var candidate in EntityIdCandidates)
            if (TryGetPropertyCaseInsensitive(targetElement, candidate, out var valueElement))
            {
                var value = ExtractScalar(valueElement);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }

        return string.Empty;
    }

    private static JsonElement? NormalizeMeta(JsonElement? meta)
    {
        if (meta is null) return null;

        var value = meta.Value;
        if (value.ValueKind == JsonValueKind.String)
        {
            var raw = value.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    return doc.RootElement.Clone();
                }
                catch (JsonException)
                {
                    // fall through to return original value
                }
        }

        return value;
    }

    private static (string UserId, string UserName) ResolveUserInfo(JsonElement? meta)
    {
        if (meta is null || meta.Value.ValueKind != JsonValueKind.Object)
            return (string.Empty, string.Empty);

        var root = meta.Value;

        if (TryExtractUserInfoFromObject(root, out var directUserId, out var directUserName))
            return (directUserId, directUserName);

        if (TryExtractFromPayloadProto(root, out var protoUserId, out var protoUserName))
            return (protoUserId, protoUserName);

        return (string.Empty, string.Empty);
    }
    
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ExtractEnumSnapshots(JsonElement? meta)
    {
        var emptyResult = new Dictionary<string, IReadOnlyDictionary<string, string>>();

        if (meta is null || meta.Value.ValueKind != JsonValueKind.Object)
            return emptyResult;

        var root = meta.Value;
        
        if (TryGetPropertyCaseInsensitive(root, "payload", out var payloadElement) &&
            payloadElement.ValueKind == JsonValueKind.String)
        {
            var base64 = payloadElement.GetString();
            if (!string.IsNullOrWhiteSpace(base64))
            {
                try
                {
                    var envelope = AuditMetaEnvelope.Parser.ParseFrom(Convert.FromBase64String(base64));
                    if (envelope.EnumSnapshots.Count > 0)
                    {
                        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>();
                        foreach (var snapshot in envelope.EnumSnapshots)
                        {
                            var pairs = new Dictionary<string, string>();
                            foreach (var pair in snapshot.Pairs)
                            {
                                pairs[pair.Code] = pair.Label;
                            }
                            result[snapshot.EnumType] = pairs;
                        }
                        return result;
                    }
                }
                catch (Exception)
                {
                    // not a valid protobuf payload – skip
                }
            }
        }

        return emptyResult;
    }

    private static bool TryExtractUserInfoFromObject(JsonElement obj, out string userId, out string userName)
    {
        userId = string.Empty;
        userName = string.Empty;

        if (TryGetPropertyCaseInsensitive(obj, "user_id", out var userIdElement) ||
            TryGetPropertyCaseInsensitive(obj, "userId", out userIdElement))
        {
            userId = ExtractScalar(userIdElement);
        }

        if (TryGetPropertyCaseInsensitive(obj, "user_name", out var userNameElement) ||
            TryGetPropertyCaseInsensitive(obj, "userName", out userNameElement))
        {
            userName = ExtractScalar(userNameElement);
        }

        if (TryGetPropertyCaseInsensitive(obj, "actor", out var actorElement) &&
            actorElement.ValueKind == JsonValueKind.Object)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                if (TryGetPropertyCaseInsensitive(actorElement, "user_id", out var actorUser) ||
                    TryGetPropertyCaseInsensitive(actorElement, "userId", out actorUser))
                {
                    userId = ExtractScalar(actorUser);
                }
            }

            if (string.IsNullOrWhiteSpace(userName))
            {
                if (TryGetPropertyCaseInsensitive(actorElement, "user_name", out var actorName) ||
                    TryGetPropertyCaseInsensitive(actorElement, "userName", out actorName))
                {
                    userName = ExtractScalar(actorName);
                }
            }
        }

        return !string.IsNullOrWhiteSpace(userId);
    }

    private static bool TryExtractFromPayloadProto(JsonElement obj, out string userId, out string userName)
    {
        userId = string.Empty;
        userName = string.Empty;

        if (TryGetPropertyCaseInsensitive(obj, "payload", out var payloadElement) &&
            payloadElement.ValueKind == JsonValueKind.String)
        {
            var base64 = payloadElement.GetString();
            if (!string.IsNullOrWhiteSpace(base64))
                try
                {
                    var envelope = AuditMetaEnvelope.Parser.ParseFrom(Convert.FromBase64String(base64));
                    userId = envelope.Actor?.UserId ?? string.Empty;
                    userName = envelope.Actor?.UserName ?? string.Empty;
                    return !string.IsNullOrWhiteSpace(userId);
                }
                catch (Exception)
                {
                    // not a valid protobuf payload – skip
                }
        }

        return false;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var property in obj.EnumerateObject())
            if (property.NameEquals(name) || property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }

        value = default;
        return false;
    }

    private static string ExtractScalar(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt64(out var longValue)
                ? longValue.ToString()
                : element.TryGetDouble(out var dbl)
                    ? dbl.ToString(CultureInfo.InvariantCulture)
                    : element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.GetRawText()
        };
    }

    private uint ResolveNormalizationVersion()
    {
        try
        {
            var version = _configurationService?.GetPolicy()?.Version;
            if (!string.IsNullOrWhiteSpace(version) && uint.TryParse(version, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            {
                return parsed;
            }
            return 1;
        }
        catch
        {
            return 1;
        }
    }

    private static string Truncate(string value, int maxLength = 256)
    {
        if (value.Length <= maxLength) return value;

        return value.Substring(0, maxLength) + "...";
    }
    
    private static IReadOnlyDictionary<(string entityType, string fieldName), ReferenceSnapshotInfo> ExtractReferenceSnapshots(JsonElement? meta)
    {
        var emptyResult = new Dictionary<(string, string), ReferenceSnapshotInfo>();

        if (meta is null || meta.Value.ValueKind != JsonValueKind.Object)
            return emptyResult;

        var root = meta.Value;
        
        if (TryGetPropertyCaseInsensitive(root, "payload", out var payloadElement) &&
            payloadElement.ValueKind == JsonValueKind.String)
        {
            var base64 = payloadElement.GetString();
            if (!string.IsNullOrWhiteSpace(base64))
            {
                try
                {
                    var envelope = AuditMetaEnvelope.Parser.ParseFrom(Convert.FromBase64String(base64));
                    if (envelope.ReferenceSnapshots.Count > 0)
                    {
                        var result = new Dictionary<(string, string), ReferenceSnapshotInfo>();
                        foreach (var snapshot in envelope.ReferenceSnapshots)
                        {
                            var key = (snapshot.EntityType, snapshot.FieldName);
                            if (!result.TryGetValue(key, out var info))
                            {
                                info = new ReferenceSnapshotInfo(
                                    snapshot.EntityType,
                                    snapshot.FieldName,
                                    snapshot.RelatedEntityType,
                                    new Dictionary<string, string>());
                                result[key] = info;
                            }
                            // Add key->title mapping
                            info.KeyTitles[snapshot.Key] = snapshot.Title;
                        }
                        return result;
                    }
                }
                catch (Exception)
                {
                    // not a valid protobuf payload – skip
                }
            }
        }

        return emptyResult;
    }

    private static IReadOnlyList<CollectionDeltaInfo> ExtractCollectionDeltas(JsonElement? meta)
    {
        var emptyResult = Array.Empty<CollectionDeltaInfo>();

        if (meta is null || meta.Value.ValueKind != JsonValueKind.Object)
            return emptyResult;

        var root = meta.Value;
        
        if (TryGetPropertyCaseInsensitive(root, "payload", out var payloadElement) &&
            payloadElement.ValueKind == JsonValueKind.String)
        {
            var base64 = payloadElement.GetString();
            if (!string.IsNullOrWhiteSpace(base64))
            {
                try
                {
                    var envelope = AuditMetaEnvelope.Parser.ParseFrom(Convert.FromBase64String(base64));
                    if (envelope.CollectionDeltas.Count > 0)
                    {
                        var result = new List<CollectionDeltaInfo>();
                        foreach (var delta in envelope.CollectionDeltas)
                        {
                            var added = delta.Added
                                .Select(a => new CollectionItemInfo(a.Key, a.Title))
                                .ToList();
                            var removed = delta.Removed
                                .Select(r => new CollectionItemInfo(r.Key, r.Title))
                                .ToList();

                            result.Add(new CollectionDeltaInfo(
                                delta.EntityType,
                                delta.EntityId,
                                delta.FieldName,
                                delta.RelatedEntityType,
                                added,
                                removed));
                        }
                        return result;
                    }
                }
                catch (Exception)
                {
                    // not a valid protobuf payload – skip
                }
            }
        }

        return emptyResult;
    }
}

internal sealed record ReferenceSnapshotInfo(
    string EntityType,
    string FieldName,
    string RelatedEntityType,
    Dictionary<string, string> KeyTitles);

internal sealed record CollectionItemInfo(string Key, string Title);

internal sealed record CollectionDeltaInfo(
    string EntityType,
    string EntityId,
    string FieldName,
    string RelatedEntityType,
    List<CollectionItemInfo> Added,
    List<CollectionItemInfo> Removed);
