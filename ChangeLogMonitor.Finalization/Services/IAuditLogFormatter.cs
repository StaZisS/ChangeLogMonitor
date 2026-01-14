using ChangeLogMonitor.Finalization.Models;

namespace ChangeLogMonitor.Finalization.Services;

public interface IAuditLogFormatter
{
    FormattedDiffResponse Format(AuditLogRecord record, string timezone);
}
