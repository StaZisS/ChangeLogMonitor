using ChangeLogMonitor.Core.Enums;

namespace ChangeLogMonitor.Finalization.Models;

public sealed record DiffFilterRequest(
    string? TableName,
    DateTime? FromTime,
    DateTime? ToTime,
    OperationCode? Operation,
    string? UserId,
    string? EntityId,
    string? TransactionId)
{
    public IReadOnlyList<string>? AllowedTableNames { get; init; }

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(TableName) ||
        FromTime.HasValue ||
        ToTime.HasValue ||
        Operation.HasValue ||
        !string.IsNullOrWhiteSpace(UserId) ||
        !string.IsNullOrWhiteSpace(EntityId) ||
        !string.IsNullOrWhiteSpace(TransactionId);

    public bool HasAccessControlFilters =>
        AllowedTableNames != null && AllowedTableNames.Count > 0;
}