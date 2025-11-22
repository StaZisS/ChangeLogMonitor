using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChangeLogMonitor.Finalization.Models;

internal sealed class StoredPayload
{
    [JsonPropertyName("data")] public JsonElement Data { get; init; }

    [JsonPropertyName("meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Meta { get; init; }

    [JsonPropertyName("incomplete")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Incomplete { get; init; }
}