using ChangeLogMonitor.UI.Models;
using ChangeLogMonitor.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChangeLogMonitor.UI.Pages.AuditLog;

public class IndexModel : PageModel
{
    private readonly IAuditLogViewService _viewService;

    public IndexModel(IAuditLogViewService viewService)
    {
        _viewService = viewService;
    }

    [BindProperty(SupportsGet = true)]
    public string? TableName { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FromTime { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToTime { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Operation { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EntityId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TransactionId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Cursor { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 50;

    public AuditLogListViewModel ViewModel { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var filter = new AuditLogFilterViewModel
        {
            TableName = TableName,
            FromTime = FromTime,
            ToTime = ToTime,
            Operation = Operation,
            UserId = UserId,
            EntityId = EntityId,
            TransactionId = TransactionId
        };

        var limit = Math.Clamp(PageSize, 10, 100);
        ViewModel = await _viewService.GetLogsAsync(filter, Cursor, limit, cancellationToken);
    }

    public ParsedPayload GetParsedPayload(string json)
    {
        return _viewService.ParsePayload(json);
    }

    public string BuildNextPageUrl()
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrWhiteSpace(TableName))
            queryParams.Add($"tableName={Uri.EscapeDataString(TableName)}");
        if (FromTime.HasValue)
            queryParams.Add($"fromTime={FromTime.Value:o}");
        if (ToTime.HasValue)
            queryParams.Add($"toTime={ToTime.Value:o}");
        if (Operation.HasValue)
            queryParams.Add($"operation={Operation.Value}");
        if (!string.IsNullOrWhiteSpace(UserId))
            queryParams.Add($"userId={Uri.EscapeDataString(UserId)}");
        if (!string.IsNullOrWhiteSpace(EntityId))
            queryParams.Add($"entityId={Uri.EscapeDataString(EntityId)}");
        if (!string.IsNullOrWhiteSpace(TransactionId))
            queryParams.Add($"transactionId={Uri.EscapeDataString(TransactionId)}");
        if (!string.IsNullOrWhiteSpace(ViewModel.NextCursor))
            queryParams.Add($"cursor={Uri.EscapeDataString(ViewModel.NextCursor)}");
        if (PageSize != 50)
            queryParams.Add($"pageSize={PageSize}");

        return queryParams.Count > 0 ? $"?{string.Join("&", queryParams)}" : "";
    }
}
