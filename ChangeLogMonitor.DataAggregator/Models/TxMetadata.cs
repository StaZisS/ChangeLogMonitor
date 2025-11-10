using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChangeLogMonitor.DataAggregator.Models;

public sealed class TxMetadata
{
    [JsonPropertyName("tx_id")]
    public string TxId { get; init; } = string.Empty;

    [JsonPropertyName("expected_total")]
    public int? ExpectedTotal { get; init; }

    [JsonPropertyName("expected_by_table")]
    public Dictionary<string, int>? ExpectedByTable { get; init; }

    [JsonPropertyName("ordering")]
    public string? Ordering { get; init; }

    [JsonPropertyName("meta")]
    public JsonElement? Meta { get; init; }
}
