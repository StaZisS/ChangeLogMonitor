using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChangeLogMonitor.DataAggregator.Models;

public sealed class TxAggregate
{
    [JsonPropertyName("tx_id")]
    public string TxId { get; init; } = string.Empty;

    [JsonPropertyName("meta")]
    public JsonElement? Meta { get; init; }

    [JsonPropertyName("incomplete")]
    public bool Incomplete { get; init; }

    [JsonPropertyName("events")]
    public IReadOnlyList<TxAggregateEvent> Events { get; init; } = Array.Empty<TxAggregateEvent>();

    [JsonPropertyName("stats")]
    public TxAggregateStats Stats { get; init; } = new();
}

public sealed class TxAggregateEvent
{
    [JsonPropertyName("table")]
    public string Table { get; init; } = string.Empty;

    [JsonPropertyName("op")]
    public string Operation { get; init; } = string.Empty;

    [JsonPropertyName("ts_ms")]
    public long TimestampMs { get; init; }

    [JsonPropertyName("total_order")]
    public long? TotalOrder { get; init; }

    [JsonPropertyName("payload")]
    public JsonElement Payload { get; init; }
}

public sealed class TxAggregateStats
{
    [JsonPropertyName("expected_total")]
    public int? ExpectedTotal { get; init; }

    [JsonPropertyName("received_total")]
    public int ReceivedTotal { get; init; }

    [JsonPropertyName("expected_by_table")]
    public IReadOnlyDictionary<string, int>? ExpectedByTable { get; init; }

    [JsonPropertyName("received_by_table")]
    public IReadOnlyDictionary<string, int> ReceivedByTable { get; init; } =
        new Dictionary<string, int>();
}
