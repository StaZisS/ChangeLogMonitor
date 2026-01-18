using System.Text.Json.Serialization;

namespace ChangeLogMonitor.Finalization.Models;

public sealed record FormattedDiffResponse(
    [property: JsonPropertyName("log_id")] long LogId,
    [property: JsonPropertyName("change_time")]
    DateTime ChangeTime,
    [property: JsonPropertyName("table_name")]
    string TableName,
    [property: JsonPropertyName("operation")]
    string Operation,
    [property: JsonPropertyName("entity_id")]
    string EntityId,
    [property: JsonPropertyName("entity_title")]
    string EntityTitle,
    [property: JsonPropertyName("tx_id")] string TransactionId,
    [property: JsonPropertyName("user_id")]
    string UserId,
    [property: JsonPropertyName("user_title")]
    string UserTitle,
    [property: JsonPropertyName("summary")]
    string Summary,
    [property: JsonPropertyName("details")]
    IReadOnlyList<string> Details);