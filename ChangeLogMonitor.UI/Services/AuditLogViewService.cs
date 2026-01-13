using System.Text.Json;
using ChangeLogMonitor.Core.Models;
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

    public AuditLogViewService(IDiffService diffService)
    {
        _diffService = diffService;
    }

    public async Task<AuditLogListViewModel> GetLogsAsync(
        AuditLogFilterViewModel filter,
        string? cursor,
        int limit,
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

        var result = await _diffService.GetFilteredAsync(filterRequest, cursor, limit, cancellationToken);

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

    private static AuditLogItemViewModel MapToViewModel(DiffResponse response)
    {
        var (operationName, badgeClass) = GetOperationInfo(response.Operation);

        return new AuditLogItemViewModel
        {
            LogId = response.LogId,
            ChangeTime = response.ChangeTime,
            TableName = response.TableName,
            Operation = response.Operation,
            OperationName = operationName,
            OperationBadgeClass = badgeClass,
            EntityId = response.EntityId,
            TransactionId = response.TransactionId,
            UserId = response.UserId,
            PayloadJson = response.Payload
        };
    }

    private static (string Name, string BadgeClass) GetOperationInfo(int operation)
    {
        return operation switch
        {
            0 => ("CREATE", "badge-create"),
            1 => ("UPDATE", "badge-update"),
            2 => ("DELETE", "badge-delete"),
            3 => ("BULK UPDATE", "badge-update"),
            4 => ("BULK DELETE", "badge-delete"),
            _ => ("UNKNOWN", "bg-secondary")
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
