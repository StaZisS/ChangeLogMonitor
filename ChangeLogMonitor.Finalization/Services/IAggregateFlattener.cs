using ChangeLogMonitor.Finalization.Models;

namespace ChangeLogMonitor.Finalization.Services;

public interface IAggregateFlattener
{
    IReadOnlyCollection<AuditLogRecord> Flatten(string? rawPayload, CancellationToken cancellationToken);
}