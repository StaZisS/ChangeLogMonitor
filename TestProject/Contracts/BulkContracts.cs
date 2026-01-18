using TestProject.Domain;

namespace TestProject.Contracts;

public record BatchUpdateStatusRequest(
    OrderStatus FromStatus,
    OrderStatus ToStatus);

public record BatchDeleteByStatusRequest(
    OrderStatus Status);

public record RawUpdateRequest(
    decimal MinAmount,
    decimal MaxAmount,
    OrderStatus NewStatus,
    string? Reason = null);

public record BulkOperationResult(
    int AffectedCount,
    string Operation,
    string? Reason = null);