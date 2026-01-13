using ChangeLogMonitor.UI.Models;

namespace ChangeLogMonitor.UI.Services;

public interface IAuditLogViewService
{
    Task<AuditLogListViewModel> GetLogsAsync(
        AuditLogFilterViewModel filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken);

    ParsedPayload ParsePayload(string json);
}
