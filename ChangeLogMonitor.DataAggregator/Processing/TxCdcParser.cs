using System.Text.Json;
using ChangeLogMonitor.DataAggregator.Models;

namespace ChangeLogMonitor.DataAggregator.Processing;

internal static class TxCdcParser
{
    public static string? ExtractTxId(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            return ExtractTxId(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool TryParse(
        string rawJson,
        string txId,
        string eventUid,
        out TxBucketEvent bucketEvent)
    {
        bucketEvent = default!;

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;
            var payload = ResolvePayload(root);

            var operation = payload.TryGetProperty("op", out var opElement)
                ? opElement.GetString() ?? string.Empty
                : string.Empty;

            var tsMs = payload.TryGetProperty("ts_ms", out var tsElement) && tsElement.TryGetInt64(out var ts)
                ? ts
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var sourceTable = payload.TryGetProperty("source", out var sourceElement) &&
                              sourceElement.TryGetProperty("table", out var tableElement)
                ? tableElement.GetString() ?? string.Empty
                : string.Empty;

            long? totalOrder = null;
            if (payload.TryGetProperty("transaction", out var txElement) &&
                txElement.TryGetProperty("total_order", out var orderElement) &&
                orderElement.TryGetInt64(out var totalOrderValue))
            {
                totalOrder = totalOrderValue;
            }

            var payloadElement = payload.TryGetProperty("after", out var afterElement) &&
                                 afterElement.ValueKind != JsonValueKind.Null &&
                                 afterElement.ValueKind != JsonValueKind.Undefined
                ? afterElement
                : payload.TryGetProperty("before", out var beforeElement)
                    ? beforeElement
                    : default;

            var payloadJson = payloadElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? "{}"
                : payloadElement.GetRawText();

            bucketEvent = new TxBucketEvent(
                sourceTable,
                operation,
                tsMs,
                totalOrder,
                payloadJson,
                eventUid);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ExtractTxId(JsonElement root)
    {
        var payload = ResolvePayload(root);

        if (payload.ValueKind == JsonValueKind.Object)
        {
            // 1) Приоритет — Debezium transaction.id (идентификатор DB-транзакции)
            if (payload.TryGetProperty("transaction", out var transaction) &&
                transaction.TryGetProperty("id", out var txIdElement))
            {
                var txId = txIdElement.GetString();
                if (!string.IsNullOrWhiteSpace(txId))
                {
                    // Debezium PostgreSQL формирует id как "xid:lsn", группируем только по xid
                    var colonIndex = txId.IndexOf(':');
                    return colonIndex > 0 ? txId.Substring(0, colonIndex) : txId;
                }
            }

            // 2) Fallback — бизнесовый transaction_id в after/before (если Debezium id отсутствует)
            if (payload.TryGetProperty("after", out var after) &&
                after.ValueKind != JsonValueKind.Null &&
                after.TryGetProperty("transaction_id", out var afterTxnId) &&
                afterTxnId.ValueKind != JsonValueKind.Null &&
                afterTxnId.ValueKind != JsonValueKind.Undefined)
            {
                var txId = afterTxnId.GetString() ?? afterTxnId.GetRawText().Trim('"');
                if (!string.IsNullOrWhiteSpace(txId))
                {
                    return txId;
                }
            }

            if (payload.TryGetProperty("before", out var before) &&
                before.ValueKind != JsonValueKind.Null &&
                before.TryGetProperty("transaction_id", out var beforeTxnId) &&
                beforeTxnId.ValueKind != JsonValueKind.Null &&
                beforeTxnId.ValueKind != JsonValueKind.Undefined)
            {
                var txId = beforeTxnId.GetString() ?? beforeTxnId.GetRawText().Trim('"');
                if (!string.IsNullOrWhiteSpace(txId))
                {
                    return txId;
                }
            }
        }

        return null;
    }

    private static JsonElement ResolvePayload(JsonElement root) =>
        root.TryGetProperty("payload", out var payload) &&
        payload.ValueKind != JsonValueKind.Null &&
        payload.ValueKind != JsonValueKind.Undefined
            ? payload
            : root;
}
