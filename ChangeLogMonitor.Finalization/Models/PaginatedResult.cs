using System.Text.Json.Serialization;

namespace ChangeLogMonitor.Finalization.Models;

public sealed record PaginatedResult<T>(
    [property: JsonPropertyName("data")] IReadOnlyList<T> Data,
    [property: JsonPropertyName("pagination")]
    PaginationInfo Pagination);

public sealed record PaginationInfo(
    [property: JsonPropertyName("next_token")]
    string? NextToken,
    [property: JsonPropertyName("has_more")]
    bool HasMore,
    [property: JsonPropertyName("limit")] int Limit);