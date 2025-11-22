using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Auditmeta.Raw;
using ChangeLogMonitor.DataAggregator.Models;
using ChangeLogMonitor.Finalization.Models;

namespace ChangeLogMonitor.Finalization.Services;

internal sealed class AggregateFlattener(ILogger<AggregateFlattener> logger) : IAggregateFlattener
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

    public IReadOnlyCollection<AuditLogRecord> Flatten(string? rawPayload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawPayload)) return Array.Empty<AuditLogRecord>();

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
            return Array.Empty<AuditLogRecord>();

        var normalizedMeta = NormalizeMeta(aggregate.Meta);
        var userId = ResolveUserId(normalizedMeta);
        var records = new List<AuditLogRecord>(aggregate.Events.Count);

        foreach (var aggregateEvent in aggregate.Events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var changeTime = ResolveTimestamp(aggregateEvent.TimestampMs);
            var entityId = ResolveEntityId(aggregateEvent.Payload);
            var payload = BuildPayload(aggregateEvent.Payload, normalizedMeta, aggregate.Incomplete);
            var operation = MapOperation(aggregateEvent.Operation);
            var tableName = aggregateEvent.Table?.Trim() ?? string.Empty;

            records.Add(new AuditLogRecord(
                0,
                changeTime,
                userId,
                tableName,
                operation,
                entityId,
                aggregate.TxId,
                payload));
        }

        return records;
    }

    private static DateTime ResolveTimestamp(long timestampMs)
    {
        var ts = timestampMs > 0 ? timestampMs : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return DateTimeOffset.FromUnixTimeMilliseconds(ts).UtcDateTime;
    }

    private static byte MapOperation(string rawOp)
    {
        var op = rawOp?.Trim();
        if (string.IsNullOrWhiteSpace(op)) return 0;

        return char.ToLowerInvariant(op[0]) switch
        {
            'c' => 1, // insert/created
            'r' => 1, // snapshot/read is treated as insert
            'u' => 2, // update
            'd' => 3, // delete
            _ => 0
        };
    }

    private static string ResolveEntityId(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return string.Empty;

        foreach (var candidate in EntityIdCandidates)
            if (TryGetPropertyCaseInsensitive(payload, candidate, out var valueElement))
            {
                var value = ExtractScalar(valueElement);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }

        return string.Empty;
    }

    private static string BuildPayload(JsonElement payload, JsonElement? meta, bool incomplete)
    {
        JsonElement dataElement;
        if (payload.ValueKind == JsonValueKind.Undefined || payload.ValueKind == JsonValueKind.Null)
        {
            using var emptyDoc = JsonDocument.Parse("{}");
            dataElement = emptyDoc.RootElement.Clone();
        }
        else
        {
            dataElement = payload;
        }

        var storedPayload = new StoredPayload
        {
            Data = dataElement,
            Meta = meta,
            Incomplete = incomplete
        };

        return JsonSerializer.Serialize(storedPayload, PayloadSerializerOptions);
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

    private static string ResolveUserId(JsonElement? meta)
    {
        if (meta is null || meta.Value.ValueKind != JsonValueKind.Object) return string.Empty;

        var root = meta.Value;

        if (TryExtractUserIdFromObject(root, out var directUserId)) return directUserId;

        if (TryExtractFromPayloadProto(root, out var protoUserId)) return protoUserId;

        return string.Empty;
    }

    private static bool TryExtractUserIdFromObject(JsonElement obj, out string userId)
    {
        if (TryGetPropertyCaseInsensitive(obj, "user_id", out var userIdElement) ||
            TryGetPropertyCaseInsensitive(obj, "userId", out userIdElement))
        {
            userId = ExtractScalar(userIdElement);
            if (!string.IsNullOrWhiteSpace(userId)) return true;
        }

        if (TryGetPropertyCaseInsensitive(obj, "actor", out var actorElement) &&
            actorElement.ValueKind == JsonValueKind.Object)
            if (TryGetPropertyCaseInsensitive(actorElement, "user_id", out var actorUser) ||
                TryGetPropertyCaseInsensitive(actorElement, "userId", out actorUser))
            {
                userId = ExtractScalar(actorUser);
                if (!string.IsNullOrWhiteSpace(userId)) return true;
            }

        userId = string.Empty;
        return false;
    }

    private static bool TryExtractFromPayloadProto(JsonElement obj, out string userId)
    {
        if (TryGetPropertyCaseInsensitive(obj, "payload", out var payloadElement) &&
            payloadElement.ValueKind == JsonValueKind.String)
        {
            var base64 = payloadElement.GetString();
            if (!string.IsNullOrWhiteSpace(base64))
                try
                {
                    var envelope = AuditMetaEnvelope.Parser.ParseFrom(Convert.FromBase64String(base64));
                    userId = envelope.Actor?.UserId ?? string.Empty;
                    return !string.IsNullOrWhiteSpace(userId);
                }
                catch (Exception)
                {
                    // not a valid protobuf payload – skip
                }
        }

        userId = string.Empty;
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

    private static string Truncate(string value, int maxLength = 256)
    {
        if (value.Length <= maxLength) return value;

        return value.Substring(0, maxLength) + "...";
    }
}