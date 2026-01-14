using System.Collections.Concurrent;
using System.Data;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Interceptor.Models;
using ChangeLogMonitor.Interceptor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ChangeLogMonitor.Interceptor.Interceptors;

/// <summary>
///     Интерцептор для перехвата изменений в базе данных и записи метаданных в audit_log
/// </summary>
public class ChangeLogInterceptor : SaveChangesInterceptor
{
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaReady;
    private readonly AuditDbContext _auditDbContext;
    private readonly IAuditConfigurationService _configService;
    private readonly ILogger<ChangeLogInterceptor>? _logger;
    private readonly AuditMetadataSerializer _metadataSerializer;
    private readonly ConcurrentDictionary<DbContext, IDbContextTransaction> _ownedTransactions = new();

    public ChangeLogInterceptor(
        AuditDbContext auditDbContext,
        IAuditConfigurationService configService,
        AuditMetadataSerializer metadataSerializer,
        ILogger<ChangeLogInterceptor>? logger = null)
    {
        _auditDbContext = auditDbContext ?? throw new ArgumentNullException(nameof(auditDbContext));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _metadataSerializer = metadataSerializer ?? throw new ArgumentNullException(nameof(metadataSerializer));
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context != null && ShouldAudit(eventData.Context))
            try
            {
                CaptureChanges(eventData.Context);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to capture audit metadata during SaveChanges");
                // В зависимости от требований можно либо пробросить исключение, либо проигнорировать
                // throw;
            }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null && ShouldAudit(eventData.Context))
            try
            {
                await CaptureChangesAsync(eventData.Context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to capture audit metadata during SaveChangesAsync");
                // В зависимости от требований можно либо пробросить исключение, либо проигнорировать
                // throw;
            }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        CommitIfStarted(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        CommitIfStarted(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        RollbackIfStarted(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        RollbackIfStarted(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <summary>
    ///     Проверяет, нужно ли аудировать данный контекст
    /// </summary>
    private bool ShouldAudit(DbContext context)
    {
        // Не аудируем сам AuditDbContext, чтобы избежать бесконечной рекурсии
        return context is not AuditDbContext;
    }

    /// <summary>
    ///     Захватывает изменения и записывает метаданные в audit_log (синхронная версия)
    /// </summary>
    private void CaptureChanges(DbContext context)
    {
        EnsureSchema();

        var entries = GetTrackedEntries(context);
        if (entries.Count == 0)
        {
            _logger?.LogDebug("No auditable changes detected");
            return;
        }

        var transactionId = GenerateTransactionId();
        var payload = _metadataSerializer.Serialize(transactionId);
        var createdAt = DateTime.UtcNow;
        WriteAuditLog(context, transactionId, payload, createdAt);

        _logger?.LogInformation(
            "Audit metadata captured. TransactionId: {TransactionId}, Entities changed: {EntityCount}",
            transactionId,
            entries.Count);
    }

    /// <summary>
    ///     Захватывает изменения и записывает метаданные в audit_log (асинхронная версия)
    /// </summary>
    private async Task CaptureChangesAsync(DbContext context, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        var entries = GetTrackedEntries(context);
        if (entries.Count == 0)
        {
            _logger?.LogDebug("No auditable changes detected");
            return;
        }

        var transactionId = GenerateTransactionId();
        var payload = _metadataSerializer.Serialize(transactionId);
        var createdAt = DateTime.UtcNow;
        await WriteAuditLogAsync(context, transactionId, payload, createdAt, cancellationToken);

        _logger?.LogInformation(
            "Audit metadata captured. TransactionId: {TransactionId}, Entities changed: {EntityCount}",
            transactionId,
            entries.Count);
    }


    /// <summary>
    ///     Получает отслеживаемые записи, которые нужно аудировать
    /// </summary>
    private List<EntityEntry> GetTrackedEntries(DbContext context)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added ||
                        e.State == EntityState.Modified ||
                        e.State == EntityState.Deleted)
            .ToList();

        // Фильтруем по конфигурации
        var auditableEntries = new List<EntityEntry>();

        foreach (var entry in entries)
        {
            // Используем имя таблицы из БД, а не имя класса C#
            var tableName = GetTableName(context, entry);

            // Проверяем включено ли логирование для этой сущности
            if (_configService.IsEntityEnabled(tableName))
            {
                auditableEntries.Add(entry);
                _logger?.LogDebug(
                    "Entity {TableName} with state {State} will be audited",
                    tableName,
                    entry.State);
            }
            else
            {
                _logger?.LogTrace(
                    "Entity {TableName} is excluded from audit by configuration",
                    tableName);
            }
        }

        return auditableEntries;
    }

    /// <summary>
    ///     Получает имя таблицы в БД для сущности
    /// </summary>
    private static string GetTableName(DbContext context, EntityEntry entry)
    {
        var entityType = context.Model.FindEntityType(entry.Entity.GetType());
        return entityType?.GetTableName() ?? entry.Entity.GetType().Name;
    }

    /// <summary>
    ///     Генерирует уникальный ID транзакции
    /// </summary>
    private string GenerateTransactionId()
    {
        // Используем Guid для уникальности
        // Можно также добавить timestamp для удобства сортировки
        return $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
    }

    private void EnsureSchema()
    {
        if (_schemaReady) return;
        EnsureSchemaAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady) return;

        await SchemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaReady) return;

            if (await AuditTableExistsAsync(cancellationToken))
            {
                _schemaReady = true;
                return;
            }

            // Если миграций нет, считаем схему готовой
            var pending = await _auditDbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            if (!pending.Any())
            {
                _schemaReady = true;
                return;
            }

            await _auditDbContext.Database.MigrateAsync(cancellationToken);
            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private async Task<bool> AuditTableExistsAsync(CancellationToken cancellationToken)
    {
        await using var connection = _auditDbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass('public.audit_log')::text";
        var result = await command.ExecuteScalarAsync(cancellationToken) as string;
        return !string.IsNullOrEmpty(result);
    }

    private AuditDbContext CreateAuditDbContextWithTransaction(DbContext appContext)
    {
        EnsureTransaction(appContext);

        var auditOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(appContext.Database.GetDbConnection())
            .Options;

        var auditContext = new AuditDbContext(auditOptions);
        auditContext.Database.UseTransaction(appContext.Database.CurrentTransaction!.GetDbTransaction());
        return auditContext;
    }

    private void CommitIfStarted(DbContext? context)
    {
        if (context == null) return;
        if (_ownedTransactions.TryRemove(context, out var tx))
        {
            tx.Commit();
            tx.Dispose();
        }
    }

    private void RollbackIfStarted(DbContext? context)
    {
        if (context == null) return;
        if (_ownedTransactions.TryRemove(context, out var tx))
        {
            tx.Rollback();
            tx.Dispose();
        }
    }

    private void EnsureTransaction(DbContext context)
    {
        if (context.Database.CurrentTransaction != null) return;

        var tx = context.Database.BeginTransaction();
        _ownedTransactions[context] = tx;
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
}