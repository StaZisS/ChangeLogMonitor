using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLogMonitor.DataAggregator.Models;

namespace ChangeLogMonitor.DataAggregator.Processing;

internal static class TxAggregateBuilder
{
    private static readonly JsonSerializerOptions OutputSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null
    };

    public static string BuildAggregate(TxBucket bucket, bool incomplete)
    {
        var ordering = DetermineOrdering(bucket);

        var orderedEvents = bucket.Events
            .OrderBy(e => ordering == TxOrdering.TotalOrder ? e.TotalOrder ?? long.MaxValue : e.TimestampMs)
            .ThenBy(e => e.TimestampMs)
            .Select(e => new TxAggregateEvent
            {
                Table = e.Table,
                Operation = e.Operation,
                TimestampMs = e.TimestampMs,
                TotalOrder = e.TotalOrder,
                Payload = e.PayloadAsJson()
            })
            .ToList();

        var aggregate = new TxAggregate
        {
            TxId = bucket.TxId,
            Incomplete = incomplete || bucket.ExceededMaxEvents,
            Meta = bucket.MetaAsJson(),
            Events = orderedEvents,
            Stats = new TxAggregateStats
            {
                ExpectedTotal = bucket.ExpectedTotal,
                ReceivedTotal = bucket.ReceivedTotal,
                ExpectedByTable = bucket.ExpectedByTable != null
                    ? new Dictionary<string, int>(bucket.ExpectedByTable, StringComparer.OrdinalIgnoreCase)
                    : null,
                ReceivedByTable = new Dictionary<string, int>(bucket.ReceivedByTable, StringComparer.OrdinalIgnoreCase)
            }
        };

        return JsonSerializer.Serialize(aggregate, OutputSerializerOptions);
    }

    private static TxOrdering DetermineOrdering(TxBucket bucket)
    {
        var requested = bucket.Ordering?.Trim().ToLowerInvariant();
        return requested switch
        {
            "ts_ms" => TxOrdering.Timestamp,
            "total_order" => TxOrdering.TotalOrder,
            _ => bucket.Events.Any(e => e.TotalOrder.HasValue)
                ? TxOrdering.TotalOrder
                : TxOrdering.Timestamp
        };
    }

    private enum TxOrdering
    {
        TotalOrder,
        Timestamp
    }
}
