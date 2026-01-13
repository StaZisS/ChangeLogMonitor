namespace ChangeLogMonitor.Finalization.Models;

public sealed record DiffFilterRequest(
    string? TableName,
    DateTime? FromTime,
    DateTime? ToTime,
    int? Operation,
    string? UserId,
    string? EntityId,
    string? TransactionId)
{
    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(TableName) ||
        FromTime.HasValue ||
        ToTime.HasValue ||
        Operation.HasValue ||
        !string.IsNullOrWhiteSpace(UserId) ||
        !string.IsNullOrWhiteSpace(EntityId) ||
        !string.IsNullOrWhiteSpace(TransactionId);
}
