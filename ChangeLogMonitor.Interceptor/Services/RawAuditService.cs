using ChangeLogMonitor.Interceptor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ChangeLogMonitor.Interceptor.Services;

/// <summary>
///     Реализация сервиса для записи метаданных аудита для raw SQL операций
/// </summary>
public class RawAuditService : IRawAuditService
{
    private readonly AuditDbContext _auditDbContext;
    private readonly ILogger<RawAuditService>? _logger;
    private readonly AuditMetadataSerializer _metadataSerializer;

    public RawAuditService(
        AuditDbContext auditDbContext,
        AuditMetadataSerializer metadataSerializer,
        ILogger<RawAuditService>? logger = null)
    {
        _auditDbContext = auditDbContext ?? throw new ArgumentNullException(nameof(auditDbContext));
        _metadataSerializer = metadataSerializer ?? throw new ArgumentNullException(nameof(metadataSerializer));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecordRawOperationAsync(
        DbContext context,
        string targetEntity,
        int affectedCount,
        string? reason = null,
        Dictionary<string, string>? hints = null,
        Dictionary<string, Dictionary<string, string>>? enumSnapshots = null,
        CancellationToken cancellationToken = default)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrWhiteSpace(targetEntity)) throw new ArgumentNullException(nameof(targetEntity));

        var allHints = MergeHints(reason, hints);
        var transactionId = GenerateTransactionId();
        var payload = _metadataSerializer.SerializeBulkOperation(transactionId, targetEntity, affectedCount, allHints, enumSnapshots);
        var createdAt = DateTime.UtcNow;

        await WriteAuditLogAsync(context, transactionId, payload, createdAt, cancellationToken);

        _logger?.LogInformation(
            "Raw operation audit recorded. TransactionId: {TransactionId}, Target: {Target}, AffectedCount: {AffectedCount}",
            transactionId,
            targetEntity,
            affectedCount);
    }

    /// <inheritdoc />
    public void RecordRawOperation(
        DbContext context,
        string targetEntity,
        int affectedCount,
        string? reason = null,
        Dictionary<string, string>? hints = null,
        Dictionary<string, Dictionary<string, string>>? enumSnapshots = null)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrWhiteSpace(targetEntity)) throw new ArgumentNullException(nameof(targetEntity));

        var allHints = MergeHints(reason, hints);
        var transactionId = GenerateTransactionId();
        var payload = _metadataSerializer.SerializeBulkOperation(transactionId, targetEntity, affectedCount, allHints, enumSnapshots);
        var createdAt = DateTime.UtcNow;

        WriteAuditLog(context, transactionId, payload, createdAt);

        _logger?.LogInformation(
            "Raw operation audit recorded. TransactionId: {TransactionId}, Target: {Target}, AffectedCount: {AffectedCount}",
            transactionId,
            targetEntity,
            affectedCount);
    }

    /// <inheritdoc />
    public async Task RecordRawOperationFromScopeAsync(
        DbContext context,
        int affectedCount,
        CancellationToken cancellationToken = default)
    {
        var scope = AuditScope.Current
                    ?? throw new InvalidOperationException(
                        "No active AuditScope. Use AuditScope.Begin() to create a scope before calling this method.");

        var targetEntity = scope.GetEffectiveTargetEntity()
                           ?? throw new InvalidOperationException(
                               "TargetEntity is not set in the current AuditScope. Use SetTargetEntity() to set it.");

        var reason = scope.GetEffectiveReason();
        var hints = scope.GetAllHints();

        await RecordRawOperationAsync(context, targetEntity, affectedCount, reason, hints, null, cancellationToken);
    }

    /// <inheritdoc />
    public void RecordRawOperationFromScope(
        DbContext context,
        int affectedCount)
    {
        var scope = AuditScope.Current
                    ?? throw new InvalidOperationException(
                        "No active AuditScope. Use AuditScope.Begin() to create a scope before calling this method.");

        var targetEntity = scope.GetEffectiveTargetEntity()
                           ?? throw new InvalidOperationException(
                               "TargetEntity is not set in the current AuditScope. Use SetTargetEntity() to set it.");

        var reason = scope.GetEffectiveReason();
        var hints = scope.GetAllHints();

        RecordRawOperation(context, targetEntity, affectedCount, reason, hints, null);
    }

    private static string GenerateTransactionId()
    {
        return $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
    }

    private static Dictionary<string, string>? MergeHints(string? reason, Dictionary<string, string>? hints)
    {
        // Собираем hints из AuditScope если есть
        var scopeHints = AuditScope.Current?.GetAllHints();
        var scopeReason = AuditScope.Current?.GetEffectiveReason();

        var allHints = new Dictionary<string, string>();

        // Добавляем hints из scope
        if (scopeHints != null)
            foreach (var hint in scopeHints)
                allHints[hint.Key] = hint.Value;

        // Добавляем переданные hints (перезаписывают scope)
        if (hints != null)
            foreach (var hint in hints)
                allHints[hint.Key] = hint.Value;

        // Добавляем reason как hint если задан
        var effectiveReason = reason ?? scopeReason;
        if (!string.IsNullOrWhiteSpace(effectiveReason))
            allHints["_reason"] = effectiveReason;

        return allHints.Count > 0 ? allHints : null;
    }

    private void WriteAuditLog(DbContext appContext, string transactionId, byte[] payload, DateTime createdAt)
    {
        EnsureTransaction(appContext);

        var auditLog = new AuditLog
        {
            TransactionId = transactionId,
            Payload = payload,
            CreatedAt = createdAt
        };

        using var auditContext = CreateAuditDbContextWithTransaction(appContext);
        auditContext.AuditLogs.Add(auditLog);
        auditContext.SaveChanges();
    }

    private async Task WriteAuditLogAsync(
        DbContext appContext,
        string transactionId,
        byte[] payload,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        EnsureTransaction(appContext);

        var auditLog = new AuditLog
        {
            TransactionId = transactionId,
            Payload = payload,
            CreatedAt = createdAt
        };

        await using var auditContext = CreateAuditDbContextWithTransaction(appContext);
        await auditContext.AuditLogs.AddAsync(auditLog, cancellationToken);
        await auditContext.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureTransaction(DbContext context)
    {
        if (context.Database.CurrentTransaction == null)
            context.Database.BeginTransaction();
    }

    private AuditDbContext CreateAuditDbContextWithTransaction(DbContext appContext)
    {
        var auditOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(appContext.Database.GetDbConnection())
            .Options;

        var auditContext = new AuditDbContext(auditOptions);
        auditContext.Database.UseTransaction(appContext.Database.CurrentTransaction!.GetDbTransaction());
        return auditContext;
    }
}
