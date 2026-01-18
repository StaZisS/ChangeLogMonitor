using ChangeLogMonitor.Interceptor.Services;
using Microsoft.EntityFrameworkCore;

namespace ChangeLogMonitor.Interceptor.Extensions;

public static class DatabaseFacadeAuditExtensions
{
    /// <summary>
    ///     Выполняет raw SQL и записывает метаданные аудита
    /// </summary>
    /// <param name="context">DbContext</param>
    /// <param name="auditService">Сервис аудита</param>
    /// <param name="targetEntity">Целевая сущность/таблица</param>
    /// <param name="sql">SQL запрос</param>
    /// <param name="parameters">Параметры запроса</param>
    /// <param name="reason">Причина операции (опционально)</param>
    /// <param name="hints">Дополнительные hints (опционально)</param>
    /// <param name="enumSnapshots">Снепшоты enum значений (опционально)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Количество затронутых строк</returns>
    public static async Task<int> ExecuteSqlRawWithAuditAsync(
        this DbContext context,
        IRawAuditService auditService,
        string targetEntity,
        string sql,
        object[]? parameters = null,
        string? reason = null,
        Dictionary<string, string>? hints = null,
        Dictionary<string, Dictionary<string, string>>? enumSnapshots = null,
        CancellationToken cancellationToken = default)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (auditService == null) throw new ArgumentNullException(nameof(auditService));
        if (string.IsNullOrWhiteSpace(targetEntity)) throw new ArgumentNullException(nameof(targetEntity));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));

        var affected = parameters != null
            ? await context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken)
            : await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        await auditService.RecordRawOperationAsync(
            context,
            targetEntity,
            affected,
            reason,
            hints,
            enumSnapshots,
            cancellationToken);

        return affected;
    }

    /// <summary>
    ///     Выполняет raw SQL и записывает метаданные аудита
    /// </summary>
    /// <param name="context">DbContext</param>
    /// <param name="auditService">Сервис аудита</param>
    /// <param name="targetEntity">Целевая сущность/таблица</param>
    /// <param name="sql">SQL запрос</param>
    /// <param name="parameters">Параметры запроса</param>
    /// <param name="reason">Причина операции (опционально)</param>
    /// <param name="hints">Дополнительные hints (опционально)</param>
    /// <param name="enumSnapshots">Снепшоты enum значений (опционально)</param>
    /// <returns>Количество затронутых строк</returns>
    public static int ExecuteSqlRawWithAudit(
        this DbContext context,
        IRawAuditService auditService,
        string targetEntity,
        string sql,
        object[]? parameters = null,
        string? reason = null,
        Dictionary<string, string>? hints = null,
        Dictionary<string, Dictionary<string, string>>? enumSnapshots = null)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (auditService == null) throw new ArgumentNullException(nameof(auditService));
        if (string.IsNullOrWhiteSpace(targetEntity)) throw new ArgumentNullException(nameof(targetEntity));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));

        var affected = parameters != null
            ? context.Database.ExecuteSqlRaw(sql, parameters)
            : context.Database.ExecuteSqlRaw(sql);

        auditService.RecordRawOperation(
            context,
            targetEntity,
            affected,
            reason,
            hints,
            enumSnapshots);

        return affected;
    }

    /// <summary>
    ///     Выполняет интерполированный SQL и записывает метаданные аудита
    /// </summary>
    /// <param name="context">DbContext</param>
    /// <param name="auditService">Сервис аудита</param>
    /// <param name="targetEntity">Целевая сущность/таблица</param>
    /// <param name="sql">Интерполированный SQL запрос</param>
    /// <param name="reason">Причина операции (опционально)</param>
    /// <param name="hints">Дополнительные hints (опционально)</param>
    /// <param name="enumSnapshots">Снепшоты enum значений (опционально)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Количество затронутых строк</returns>
    public static async Task<int> ExecuteSqlInterpolatedWithAuditAsync(
        this DbContext context,
        IRawAuditService auditService,
        string targetEntity,
        FormattableString sql,
        string? reason = null,
        Dictionary<string, string>? hints = null,
        Dictionary<string, Dictionary<string, string>>? enumSnapshots = null,
        CancellationToken cancellationToken = default)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (auditService == null) throw new ArgumentNullException(nameof(auditService));
        if (string.IsNullOrWhiteSpace(targetEntity)) throw new ArgumentNullException(nameof(targetEntity));
        if (sql == null) throw new ArgumentNullException(nameof(sql));

        var affected = await context.Database.ExecuteSqlInterpolatedAsync(sql, cancellationToken);

        await auditService.RecordRawOperationAsync(
            context,
            targetEntity,
            affected,
            reason,
            hints,
            enumSnapshots,
            cancellationToken);

        return affected;
    }

    /// <summary>
    ///     Выполняет интерполированный SQL и записывает метаданные аудита
    /// </summary>
    /// <param name="context">DbContext</param>
    /// <param name="auditService">Сервис аудита</param>
    /// <param name="targetEntity">Целевая сущность/таблица</param>
    /// <param name="sql">Интерполированный SQL запрос</param>
    /// <param name="reason">Причина операции (опционально)</param>
    /// <param name="hints">Дополнительные hints (опционально)</param>
    /// <param name="enumSnapshots">Снепшоты enum значений (опционально)</param>
    /// <returns>Количество затронутых строк</returns>
    public static int ExecuteSqlInterpolatedWithAudit(
        this DbContext context,
        IRawAuditService auditService,
        string targetEntity,
        FormattableString sql,
        string? reason = null,
        Dictionary<string, string>? hints = null,
        Dictionary<string, Dictionary<string, string>>? enumSnapshots = null)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (auditService == null) throw new ArgumentNullException(nameof(auditService));
        if (string.IsNullOrWhiteSpace(targetEntity)) throw new ArgumentNullException(nameof(targetEntity));
        if (sql == null) throw new ArgumentNullException(nameof(sql));

        var affected = context.Database.ExecuteSqlInterpolated(sql);

        auditService.RecordRawOperation(
            context,
            targetEntity,
            affected,
            reason,
            hints,
            enumSnapshots);

        return affected;
    }

    /// <summary>
    ///     Выполняет raw SQL с использованием текущего AuditScope для метаданных
    /// </summary>
    /// <param name="context">DbContext</param>
    /// <param name="auditService">Сервис аудита</param>
    /// <param name="sql">SQL запрос</param>
    /// <param name="parameters">Параметры запроса</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Количество затронутых строк</returns>
    /// <exception cref="InvalidOperationException">Если нет активного AuditScope</exception>
    public static async Task<int> ExecuteSqlRawWithScopeAuditAsync(
        this DbContext context,
        IRawAuditService auditService,
        string sql,
        object[]? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (auditService == null) throw new ArgumentNullException(nameof(auditService));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));

        var affected = parameters != null
            ? await context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken)
            : await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        await auditService.RecordRawOperationFromScopeAsync(context, affected, cancellationToken);

        return affected;
    }

    /// <summary>
    ///     Выполняет raw SQL с использованием текущего AuditScope для метаданных
    /// </summary>
    /// <param name="context">DbContext</param>
    /// <param name="auditService">Сервис аудита</param>
    /// <param name="sql">SQL запрос</param>
    /// <param name="parameters">Параметры запроса</param>
    /// <returns>Количество затронутых строк</returns>
    /// <exception cref="InvalidOperationException">Если нет активного AuditScope</exception>
    public static int ExecuteSqlRawWithScopeAudit(
        this DbContext context,
        IRawAuditService auditService,
        string sql,
        object[]? parameters = null)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (auditService == null) throw new ArgumentNullException(nameof(auditService));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));

        var affected = parameters != null
            ? context.Database.ExecuteSqlRaw(sql, parameters)
            : context.Database.ExecuteSqlRaw(sql);

        auditService.RecordRawOperationFromScope(context, affected);

        return affected;
    }
}
