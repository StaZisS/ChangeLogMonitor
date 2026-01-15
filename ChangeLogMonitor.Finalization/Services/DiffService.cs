using System.Text;
using System.Text.Json;
using ChangeLogMonitor.Core.Enums;
using ChangeLogMonitor.Core.Interfaces;
using ChangeLogMonitor.Finalization.Localization;
using ChangeLogMonitor.Finalization.Models;

namespace ChangeLogMonitor.Finalization.Services;

internal sealed class DiffService : IDiffService
{
    private readonly IAuditLogRepository _repository;
    private readonly IAuditLogFormatter _formatter;
    private readonly IAccessControlService _accessControl;
    private readonly ICurrentUserService _currentUser;

    public DiffService(
        IAuditLogRepository repository,
        IAuditLogFormatter formatter,
        IAccessControlService accessControl,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _formatter = formatter;
        _accessControl = accessControl;
        _currentUser = currentUser;
    }

    /// <summary>
    ///     Применяет фильтры контроля доступа к запросу
    /// </summary>
    private DiffFilterRequest ApplyAccessControl(DiffFilterRequest? baseFilter, string? specificTableName = null)
    {
        var userId = _currentUser.GetUserId();

        // Создаем базовый фильтр если не передан
        var filter = baseFilter ?? new DiffFilterRequest(null, null, null, null, null, null, null);

        if (!_accessControl.IsEnabled)
            return filter;

        var roles = _accessControl.GetUserRoles(userId);
        var allowedEntities = _accessControl.GetAllowedEntities(roles);

        // Если запрашивается конкретная таблица - проверяем доступ
        var tableName = specificTableName ?? filter.TableName;
        if (!string.IsNullOrWhiteSpace(tableName))
        {
            if (!allowedEntities.Contains(tableName))
            {
                // Доступ запрещен - вернуть фильтр который ничего не найдет
                return filter with { AllowedTableNames = new[] { "__DENIED__" } };
            }

            return filter with
            {
                TableName = tableName
            };
        }

        // Общий запрос - ограничиваем разрешенными таблицами
        return filter with
        {
            AllowedTableNames = allowedEntities.ToList()
        };
    }

    /// <summary>
    ///     Проверяет доступ к сущности и выбрасывает исключение если доступ запрещен
    /// </summary>
    private void EnsureEntityAccess(string tableName)
    {
        if (!_accessControl.IsEnabled)
            return;

        var userId = _currentUser.GetUserId();
        if (!_accessControl.CanAccessEntity(userId, tableName))
        {
            if (_accessControl.GetUnauthorizedBehavior() == UnauthorizedBehavior.Deny)
                throw new UnauthorizedAccessException($"Access denied to entity: {tableName}");
        }
    }

    public async Task<PaginatedResult<DiffResponse>> GetAllAsync(
        string? paginationToken,
        int limit,
        CancellationToken cancellationToken)
    {
        // Применяем фильтр контроля доступа
        var filter = ApplyAccessControl(null);

        // Используем GetFilteredWithCursorAsync вместо GetAllWithCursorAsync для применения фильтра
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

    public async Task<IReadOnlyList<DiffResponse>> GetByEntityAsync(
        string tableName,
        string entityId,
        int limit,
        CancellationToken cancellationToken)
    {
        // Проверяем доступ к сущности
        EnsureEntityAccess(tableName);

        // Применяем фильтр контроля доступа
        var filter = ApplyAccessControl(
            new DiffFilterRequest(tableName, null, null, null, null, entityId, null),
            tableName);

        // Если доступ запрещен через filter - возвращаем пустой список
        if (filter.AllowedTableNames?.Contains("__DENIED__") == true)
            return Array.Empty<DiffResponse>();

        var records = await _repository.GetFilteredWithCursorAsync(filter, null, limit, cancellationToken);
        return records.Select(DiffResponse.FromRecord).ToList();
    }

    public async Task<IReadOnlyList<DiffResponse>> GetByTransactionAsync(
        string transactionId,
        int limit,
        CancellationToken cancellationToken)
    {
        // Применяем фильтр контроля доступа
        var filter = ApplyAccessControl(
            new DiffFilterRequest(null, null, null, null, null, null, transactionId));

        var records = await _repository.GetFilteredWithCursorAsync(filter, null, limit, cancellationToken);
        return records.Select(DiffResponse.FromRecord).ToList();
    }

    public async Task<PaginatedResult<DiffResponse>> GetFilteredAsync(
        DiffFilterRequest filter,
        string? paginationToken,
        int limit,
        CancellationToken cancellationToken)
    {
        // Применяем фильтр контроля доступа
        var securedFilter = ApplyAccessControl(filter, filter.TableName);

        var cursor = DecodeCursor(paginationToken);
        var records = await _repository.GetFilteredWithCursorAsync(securedFilter, cursor, limit + 1, cancellationToken);

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
        // Применяем фильтр контроля доступа
        var filter = ApplyAccessControl(null);

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

    public async Task<IReadOnlyList<FormattedDiffResponse>> GetByEntityFormattedAsync(
        string tableName,
        string entityId,
        int limit,
        string timezone,
        CancellationToken cancellationToken)
    {
        // Проверяем доступ к сущности
        EnsureEntityAccess(tableName);

        // Применяем фильтр контроля доступа
        var filter = ApplyAccessControl(
            new DiffFilterRequest(tableName, null, null, null, null, entityId, null),
            tableName);

        // Если доступ запрещен через filter - возвращаем пустой список
        if (filter.AllowedTableNames?.Contains("__DENIED__") == true)
            return Array.Empty<FormattedDiffResponse>();

        var tz = string.IsNullOrWhiteSpace(timezone) ? AuditLogMessages.DefaultTimezone : timezone;
        var records = await _repository.GetFilteredWithCursorAsync(filter, null, limit, cancellationToken);
        return records.Select(r => _formatter.Format(r, tz)).ToList();
    }

    public async Task<IReadOnlyList<FormattedDiffResponse>> GetByTransactionFormattedAsync(
        string transactionId,
        int limit,
        string timezone,
        CancellationToken cancellationToken)
    {
        // Применяем фильтр контроля доступа
        var filter = ApplyAccessControl(
            new DiffFilterRequest(null, null, null, null, null, null, transactionId));

        var tz = string.IsNullOrWhiteSpace(timezone) ? AuditLogMessages.DefaultTimezone : timezone;
        var records = await _repository.GetFilteredWithCursorAsync(filter, null, limit, cancellationToken);
        return records.Select(r => _formatter.Format(r, tz)).ToList();
    }

    public async Task<PaginatedResult<FormattedDiffResponse>> GetFilteredFormattedAsync(
        DiffFilterRequest filter,
        string? paginationToken,
        int limit,
        string timezone,
        CancellationToken cancellationToken)
    {
        // Применяем фильтр контроля доступа
        var securedFilter = ApplyAccessControl(filter, filter.TableName);

        var tz = string.IsNullOrWhiteSpace(timezone) ? AuditLogMessages.DefaultTimezone : timezone;
        var cursor = DecodeCursor(paginationToken);

        var records = await _repository.GetFilteredWithCursorAsync(securedFilter, cursor, limit + 1, cancellationToken);

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
