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

    Task<PaginatedResult<DiffResponse>> GetFilteredAsync(
        DiffFilterRequest filter,
        string? paginationToken,
        int limit,
        CancellationToken cancellationToken);

    Task<PaginatedResult<FormattedDiffResponse>> GetAllFormattedAsync(
        string? paginationToken,
        int limit,
        string timezone,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FormattedDiffResponse>> GetByEntityFormattedAsync(
        string tableName,
        string entityId,
        int limit,
        string timezone,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FormattedDiffResponse>> GetByTransactionFormattedAsync(
        string transactionId,
        int limit,
        string timezone,
        CancellationToken cancellationToken);

    Task<PaginatedResult<FormattedDiffResponse>> GetFilteredFormattedAsync(
        DiffFilterRequest filter,
        string? paginationToken,
        int limit,
        string timezone,
        CancellationToken cancellationToken);
}