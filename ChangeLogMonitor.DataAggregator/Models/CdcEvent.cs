using System.Text.Json.Serialization;

namespace ChangeLogMonitor.DataAggregator.Models;

public sealed record CdcEvent(
    [property: JsonPropertyName("tx_id")] string TxId,
    [property: JsonPropertyName("table")] string Table,
    [property: JsonPropertyName("op")] string Operation,
    [property: JsonPropertyName("ts_ms")] long TimestampMs,
    [property: JsonPropertyName("total_order")] long? TotalOrder,
    [property: JsonPropertyName("payload")] string PayloadJson,
    [property: JsonPropertyName("event_uid")] string EventUid);
