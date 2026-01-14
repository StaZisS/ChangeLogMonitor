using System.Text;
using System.Text.Json;
using ChangeLogMonitor.Finalization.Localization;
using ChangeLogMonitor.Finalization.Models;

namespace ChangeLogMonitor.Finalization.Services;

internal sealed class DiffService : IDiffService
{
    private readonly IAuditLogRepository _repository;
    private readonly IAuditLogFormatter _formatter;

    public DiffService(IAuditLogRepository repository, IAuditLogFormatter formatter)
    {
        _repository = repository;
        _formatter = formatter;
    }

    public async Task<PaginatedResult<DiffResponse>> GetAllAsync(
        string? paginationToken,
        int limit,
        CancellationToken cancellationToken)
    {
        var cursor = DecodeCursor(paginationToken);

        var records = await _repository.GetAllWithCursorAsync(cursor, limit + 1, cancellationToken);

        var hasMore = records.Count > limit;
        var resultRecords = hasMore ? records.Take(limit).ToList() : records;

        string? nextToken = null;
        if (hasMore && resultRecords.Count > 0)
        {
            var lastRecord = resultRecords[^1];
            nextToken = EncodeCursor(lastRecord.LogId);
        }

        var data = resultRecords.Select(DiffResponse.FromRecord).ToList();

        return new PaginatedResult<DiffResponse>(
            data,
            new PaginationInfo(nextToken, hasMore, limit));
    }

    public async Task<IReadOnlyList<DiffResponse>> GetByEntityAsync(
        string tableName,
        string entityId,
        int limit,
        CancellationToken cancellationToken)
    {
        var records = await _repository.GetByEntityAsync(tableName, entityId, limit, cancellationToken);
        return records.Select(DiffResponse.FromRecord).ToList();
    }

    public async Task<IReadOnlyList<DiffResponse>> GetByTransactionAsync(
        string transactionId,
        int limit,
        CancellationToken cancellationToken)
    {
        var records = await _repository.GetByTransactionAsync(transactionId, limit, cancellationToken);
        return records.Select(DiffResponse.FromRecord).ToList();
    }

    public async Task<PaginatedResult<DiffResponse>> GetFilteredAsync(
        DiffFilterRequest filter,
        string? paginationToken,
        int limit,
        CancellationToken cancellationToken)
    {
        var cursor = DecodeCursor(paginationToken);

        var records = await _repository.GetFilteredWithCursorAsync(filter, cursor, limit + 1, cancellationToken);

        var hasMore = records.Count > limit;
        var resultRecords = hasMore ? records.Take(limit).ToList() : records;

        string? nextToken = null;
        if (hasMore && resultRecords.Count > 0)
        {
            var lastRecord = resultRecords[^1];
            nextToken = EncodeCursor(lastRecord.LogId);
        }

        var data = resultRecords.Select(DiffResponse.FromRecord).ToList();

        return new PaginatedResult<DiffResponse>(
            data,
            new PaginationInfo(nextToken, hasMore, limit));
    }

    public async Task<PaginatedResult<FormattedDiffResponse>> GetAllFormattedAsync(
        string? paginationToken,
        int limit,
        string timezone,
        CancellationToken cancellationToken)
    {
        var tz = string.IsNullOrWhiteSpace(timezone) ? AuditLogMessages.DefaultTimezone : timezone;
        var cursor = DecodeCursor(paginationToken);

        var records = await _repository.GetAllWithCursorAsync(cursor, limit + 1, cancellationToken);

        var hasMore = records.Count > limit;
        var resultRecords = hasMore ? records.Take(limit).ToList() : records;

        string? nextToken = null;
        if (hasMore && resultRecords.Count > 0)
        {
            var lastRecord = resultRecords[^1];
            nextToken = EncodeCursor(lastRecord.LogId);
        }

        var data = resultRecords.Select(r => _formatter.Format(r, tz)).ToList();

        return new PaginatedResult<FormattedDiffResponse>(
            data,
            new PaginationInfo(nextToken, hasMore, limit));
    }

    public async Task<IReadOnlyList<FormattedDiffResponse>> GetByEntityFormattedAsync(
        string tableName,
        string entityId,
        int limit,
        string timezone,
        CancellationToken cancellationToken)
    {
        var tz = string.IsNullOrWhiteSpace(timezone) ? AuditLogMessages.DefaultTimezone : timezone;
        var records = await _repository.GetByEntityAsync(tableName, entityId, limit, cancellationToken);
        return records.Select(r => _formatter.Format(r, tz)).ToList();
    }

    public async Task<IReadOnlyList<FormattedDiffResponse>> GetByTransactionFormattedAsync(
        string transactionId,
        int limit,
        string timezone,
        CancellationToken cancellationToken)
    {
        var tz = string.IsNullOrWhiteSpace(timezone) ? AuditLogMessages.DefaultTimezone : timezone;
        var records = await _repository.GetByTransactionAsync(transactionId, limit, cancellationToken);
        return records.Select(r => _formatter.Format(r, tz)).ToList();
    }

    public async Task<PaginatedResult<FormattedDiffResponse>> GetFilteredFormattedAsync(
        DiffFilterRequest filter,
        string? paginationToken,
        int limit,
        string timezone,
        CancellationToken cancellationToken)
    {
        var tz = string.IsNullOrWhiteSpace(timezone) ? AuditLogMessages.DefaultTimezone : timezone;
        var cursor = DecodeCursor(paginationToken);

        var records = await _repository.GetFilteredWithCursorAsync(filter, cursor, limit + 1, cancellationToken);

        var hasMore = records.Count > limit;
        var resultRecords = hasMore ? records.Take(limit).ToList() : records;

        string? nextToken = null;
        if (hasMore && resultRecords.Count > 0)
        {
            var lastRecord = resultRecords[^1];
            nextToken = EncodeCursor(lastRecord.LogId);
        }

        var data = resultRecords.Select(r => _formatter.Format(r, tz)).ToList();

        return new PaginatedResult<FormattedDiffResponse>(
            data,
            new PaginationInfo(nextToken, hasMore, limit));
    }

    private static long? DecodeCursor(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var cursor = JsonSerializer.Deserialize<CursorPayload>(json);
            return cursor?.LogId;
        }
        catch
        {
            return null;
        }
    }

    private static string EncodeCursor(long logId)
    {
        var payload = new CursorPayload(logId);
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private sealed record CursorPayload(long LogId);
}
