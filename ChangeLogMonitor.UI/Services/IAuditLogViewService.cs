using ChangeLogMonitor.UI.Models;

namespace ChangeLogMonitor.UI.Services;

public interface IAuditLogViewService
{
    Task<AuditLogListViewModel> GetLogsAsync(
        AuditLogFilterViewModel filter,
        string? cursor,
        int limit,
        string timezone,
        CancellationToken cancellationToken);

    ParsedPayload ParsePayload(string json);
    
    IReadOnlyList<string> GetAllowedEntities();
    
    bool IsAccessControlEnabled { get; }
    
    IReadOnlyList<EntityInfo> GetAvailableEntities();
}
