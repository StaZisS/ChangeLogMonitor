using System.Text.Json;
using ChangeLogMonitor.Core.Interfaces;
using ChangeLogMonitor.Core.Models;
using ChangeLogMonitor.Finalization.Localization;
using ChangeLogMonitor.Finalization.Models;
using ChangeLogMonitor.Finalization.Services;
using ChangeLogMonitor.UI.Models;

namespace ChangeLogMonitor.UI.Services;

internal sealed class AuditLogViewService : IAuditLogViewService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IDiffService _diffService;
    private readonly IAccessControlService _accessControl;
    private readonly ICurrentUserService _currentUser;

    public AuditLogViewService(
        IDiffService diffService,
        IAccessControlService accessControl,
        ICurrentUserService currentUser)
    {
        _diffService = diffService;
        _accessControl = accessControl;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public bool IsAccessControlEnabled => _accessControl.IsEnabled;

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllowedEntities()
    {
        if (!_accessControl.IsEnabled)
            return Array.Empty<string>();

        var userId = _currentUser.GetUserId();
        var roles = _accessControl.GetUserRoles(userId);
        return _accessControl.GetAllowedEntities(roles);
    }

    public async Task<AuditLogListViewModel> GetLogsAsync(
        AuditLogFilterViewModel filter,
        string? cursor,
        int limit,
        string timezone,
        CancellationToken cancellationToken)
    {
        var filterRequest = new DiffFilterRequest(
            filter.TableName,
            filter.FromTime,
            filter.ToTime,
            filter.Operation,
            filter.UserId,
            filter.EntityId,
            filter.TransactionId);

        var tz = string.IsNullOrWhiteSpace(timezone) ? AuditLogMessages.DefaultTimezone : timezone;
        var result = await _diffService.GetFilteredFormattedAsync(filterRequest, cursor, limit, tz, cancellationToken);

        var viewModel = new AuditLogListViewModel
        {
            Filter = filter,
            NextCursor = result.Pagination.NextToken,
            HasMore = result.Pagination.HasMore,
            PageSize = limit,
            TotalShown = result.Data.Count,
            Items = result.Data.Select(MapToViewModel).ToList()
        };

        return viewModel;
    }

    public ParsedPayload ParsePayload(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ParsedPayload();

        try
        {
            var payload = JsonSerializer.Deserialize<StoredPayloadDto>(json, JsonOptions);
            if (payload?.Data == null)
                return new ParsedPayload();

            var parsed = new ParsedPayload
            {
                IsIncomplete = payload.Incomplete
            };

            if (payload.Data.TryGetProperty("field_changes", out var fieldChangesElement) ||
                payload.Data.TryGetProperty("fieldChanges", out fieldChangesElement))
            {
                parsed.FieldChanges = DeserializeList<FieldChange>(fieldChangesElement);
            }

            if (payload.Data.TryGetProperty("reference_changes", out var refChangesElement) ||
                payload.Data.TryGetProperty("referenceChanges", out refChangesElement))
            {
                parsed.ReferenceChanges = DeserializeList<ReferenceChange>(refChangesElement);
            }

            if (payload.Data.TryGetProperty("collection_changes", out var collChangesElement) ||
                payload.Data.TryGetProperty("collectionChanges", out collChangesElement))
            {
                parsed.CollectionChanges = DeserializeList<CollectionChange>(collChangesElement);
            }

            return parsed;
        }
        catch
        {
            return new ParsedPayload();
        }
    }

    private static AuditLogItemViewModel MapToViewModel(FormattedDiffResponse response)
    {
        var badgeClass = GetOperationBadgeClass(response.Operation);

        return new AuditLogItemViewModel
        {
            LogId = response.LogId,
            ChangeTime = response.ChangeTime,
            TableName = response.TableName,
            Operation = GetOperationCode(response.Operation),
            OperationName = response.Operation,
            OperationBadgeClass = badgeClass,
            EntityId = response.EntityId,
            EntityTitle = response.EntityTitle,
            TransactionId = response.TransactionId,
            UserId = response.UserId,
            UserTitle = response.UserTitle,
            PayloadJson = string.Empty,
            Summary = response.Summary,
            Details = response.Details.ToList()
        };
    }

    private static string GetOperationBadgeClass(string operation)
    {
        return operation.ToUpperInvariant() switch
        {
            "CREATE" => "badge-create",
            "UPDATE" => "badge-update",
            "DELETE" or "SOFT_DELETE" => "badge-delete",
            "BULK_UPDATE" => "badge-update",
            "BULK_DELETE" => "badge-delete",
            _ => "bg-secondary"
        };
    }

    private static int GetOperationCode(string operation)
    {
        return operation.ToUpperInvariant() switch
        {
            "CREATE" => 1,
            "UPDATE" => 2,
            "DELETE" => 3,
            "SOFT_DELETE" => 4,
            "BULK_UPDATE" => 5,
            "BULK_DELETE" => 6,
            _ => 0
        };
    }

    private static List<T> DeserializeList<T>(JsonElement element)
    {
        try
        {
            return element.Deserialize<List<T>>(JsonOptions) ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    private sealed class StoredPayloadDto
    {
        public JsonElement Data { get; set; }
        public JsonElement? Meta { get; set; }
        public bool Incomplete { get; set; }
    }
}
