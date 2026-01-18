using ChangeLogMonitor.UI.Models;

namespace ChangeLogMonitor.UI.Services;

public interface IAuditLogViewService
{
    bool IsAccessControlEnabled { get; }

    Task<AuditLogListViewModel> GetLogsAsync(
        AuditLogFilterViewModel filter,
        string? cursor,
        int limit,
        string timezone,
        CancellationToken cancellationToken);

    ParsedPayload ParsePayload(string json);

    IReadOnlyList<string> GetAllowedEntities();

    IReadOnlyList<EntityInfo> GetAvailableEntities();
}