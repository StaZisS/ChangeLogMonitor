namespace ChangeLogMonitor.UI.Models;

public class AuditLogFilterViewModel
{
    public string? TableName { get; set; }
    public DateTime? FromTime { get; set; }
    public DateTime? ToTime { get; set; }
    public int? Operation { get; set; }
    public string? UserId { get; set; }
    public string? EntityId { get; set; }
    public string? TransactionId { get; set; }

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(TableName) ||
        FromTime.HasValue ||
        ToTime.HasValue ||
        Operation.HasValue ||
        !string.IsNullOrWhiteSpace(UserId) ||
        !string.IsNullOrWhiteSpace(EntityId) ||
        !string.IsNullOrWhiteSpace(TransactionId);
}
