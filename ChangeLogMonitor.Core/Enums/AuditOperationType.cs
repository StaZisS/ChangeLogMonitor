namespace ChangeLogMonitor.Core.Enums;

/// <summary>
///     Тип операции в аудите (Create/Update/Delete)
/// </summary>
public enum AuditOperationType
{
    Create,
    Update,
    Delete,
    BulkUpdate,
    BulkDelete
}