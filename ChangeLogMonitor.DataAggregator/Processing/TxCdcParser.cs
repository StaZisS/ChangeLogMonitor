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
            var payload = document.RootElement.GetProperty("payload");

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
        if (!root.TryGetProperty("payload", out var payload))
        {
            return null;
        }

        if (payload.TryGetProperty("transaction", out var transaction) &&
            transaction.TryGetProperty("id", out var txIdElement))
        {
            var txId = txIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(txId))
            {
                return txId;
            }
        }

        if (payload.TryGetProperty("source", out var source) &&
            source.TryGetProperty("txId", out var txIdNumeric) &&
            txIdNumeric.ValueKind != JsonValueKind.Null &&
            txIdNumeric.ValueKind != JsonValueKind.Undefined)
        {
            // Debezium Postgres txId (numeric) fallback when transaction metadata is disabled
            if (txIdNumeric.TryGetInt64(out var txIdLong))
            {
                return txIdLong.ToString();
            }

            var txId = txIdNumeric.GetRawText();
            if (!string.IsNullOrWhiteSpace(txId))
            {
                return txId.Trim('"');
            }
        }

        if (payload.TryGetProperty("after", out var after) &&
            after.ValueKind != JsonValueKind.Null &&
            after.TryGetProperty("tx_id", out var afterTxId))
        {
            var txId = afterTxId.GetString();
            if (!string.IsNullOrWhiteSpace(txId))
            {
                return txId;
            }
        }

        if (payload.TryGetProperty("before", out var before) &&
            before.ValueKind != JsonValueKind.Null &&
            before.TryGetProperty("tx_id", out var beforeTxId))
        {
            var txId = beforeTxId.GetString();
            if (!string.IsNullOrWhiteSpace(txId))
            {
                return txId;
            }
        }

        return null;
    }
}
