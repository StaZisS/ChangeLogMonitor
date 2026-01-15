namespace ChangeLogMonitor.Finalization.Models;

public sealed record DiffFilterRequest(
    string? TableName,
    DateTime? FromTime,
    DateTime? ToTime,
    int? Operation,
    string? UserId,
    string? EntityId,
    string? TransactionId)
{
    /// <summary>
    ///     Список разрешенных таблиц для фильтрации по ролям.
    ///     Если null или пустой - фильтрация по таблицам не применяется.
    /// </summary>
    public IReadOnlyList<string>? AllowedTableNames { get; init; }

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(TableName) ||
        FromTime.HasValue ||
        ToTime.HasValue ||
        Operation.HasValue ||
        !string.IsNullOrWhiteSpace(UserId) ||
        !string.IsNullOrWhiteSpace(EntityId) ||
        !string.IsNullOrWhiteSpace(TransactionId);

    public bool HasAccessControlFilters =>
        AllowedTableNames != null && AllowedTableNames.Count > 0;
}
