using ChangeLogMonitor.Finalization.Models;

namespace ChangeLogMonitor.Finalization.Services;

public interface IAuditLogRepository
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken);

    Task InsertAsync(IReadOnlyCollection<AuditLogRecord> records, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditLogRecord>> GetByEntityAsync(
        string tableName,
        string entityId,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditLogRecord>> GetByTransactionAsync(
        string transactionId,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditLogRecord>> GetAllWithCursorAsync(
        long? cursorLogId,
        int limit,
        CancellationToken cancellationToken);
}