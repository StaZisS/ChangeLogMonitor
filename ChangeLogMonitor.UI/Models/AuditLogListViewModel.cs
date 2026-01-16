namespace ChangeLogMonitor.UI.Models;

public class AuditLogListViewModel
{
    public AuditLogFilterViewModel Filter { get; set; } = new();
    public List<AuditLogItemViewModel> Items { get; set; } = new();
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }
    public int PageSize { get; set; } = 50;
    public int TotalShown { get; set; }
}

public class AuditLogItemViewModel
{
    public long LogId { get; set; }
    public DateTime ChangeTime { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int Operation { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public string OperationBadgeClass { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string EntityTitle { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserTitle { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    
    public string Summary { get; set; } = string.Empty;
    public List<string> Details { get; set; } = new();
}
