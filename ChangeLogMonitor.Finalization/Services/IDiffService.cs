using ChangeLogMonitor.Finalization.Models;

namespace ChangeLogMonitor.Finalization.Services;

public interface IDiffService
{
    Task<PaginatedResult<DiffResponse>> GetAllAsync(
        string? paginationToken,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DiffResponse>> GetByEntityAsync(
        string tableName,
        string entityId,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DiffResponse>> GetByTransactionAsync(
        string transactionId,
        int limit,
        CancellationToken cancellationToken);
}
