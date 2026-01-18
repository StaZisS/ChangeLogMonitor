using Microsoft.EntityFrameworkCore;

namespace ChangeLogMonitor.Interceptor.Services;

/// <summary>
///     Сервис для записи метаданных аудита для raw SQL операций.
///     Используется когда изменения делаются через ExecuteSqlRaw, ExecuteSqlInterpolated и т.д.
/// </summary>
public interface IRawAuditService
{
    /// <summary>
    ///     Записывает метаданные для raw SQL операции (асинхронно)
    /// </summary>
    /// <param name="context">DbContext, в котором выполняется операция</param>
    /// <param name="targetEntity">Целевая сущность/таблица</param>
    /// <param name="affectedCount">Количество затронутых строк</param>
    /// <param name="reason">Причина/описание операции (опционально)</param>
    /// <param name="hints">Дополнительные hints (опционально)</param>
    /// <param name="enumSnapshots">Снепшоты enum значений: enumType -> (code -> label) (опционально)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task RecordRawOperationAsync(
        DbContext context,
        string targetEntity,
        int affectedCount,
        string? reason = null,
        Dictionary<string, string>? hints = null,
        Dictionary<string, Dictionary<string, string>>? enumSnapshots = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Записывает метаданные для raw SQL операции (синхронно)
    /// </summary>
    /// <param name="context">DbContext, в котором выполняется операция</param>
    /// <param name="targetEntity">Целевая сущность/таблица</param>
    /// <param name="affectedCount">Количество затронутых строк</param>
    /// <param name="reason">Причина/описание операции (опционально)</param>
    /// <param name="hints">Дополнительные hints (опционально)</param>
    /// <param name="enumSnapshots">Снепшоты enum значений: enumType -> (code -> label) (опционально)</param>
    void RecordRawOperation(
        DbContext context,
        string targetEntity,
        int affectedCount,
        string? reason = null,
        Dictionary<string, string>? hints = null,
        Dictionary<string, Dictionary<string, string>>? enumSnapshots = null);

    /// <summary>
    ///     Записывает метаданные для raw SQL операции, используя данные из текущего AuditScope (асинхронно)
    /// </summary>
    /// <param name="context">DbContext, в котором выполняется операция</param>
    /// <param name="affectedCount">Количество затронутых строк</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="InvalidOperationException">Если нет активного AuditScope или не задана целевая сущность</exception>
    Task RecordRawOperationFromScopeAsync(
        DbContext context,
        int affectedCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Записывает метаданные для raw SQL операции, используя данные из текущего AuditScope (синхронно)
    /// </summary>
    /// <param name="context">DbContext, в котором выполняется операция</param>
    /// <param name="affectedCount">Количество затронутых строк</param>
    /// <exception cref="InvalidOperationException">Если нет активного AuditScope или не задана целевая сущность</exception>
    void RecordRawOperationFromScope(
        DbContext context,
        int affectedCount);
}