using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Interceptor.Models;
using ChangeLogMonitor.Interceptor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace ChangeLogMonitor.Interceptor.Interceptors;

/// <summary>
/// Интерцептор для перехвата изменений в базе данных и записи метаданных в audit_log
/// </summary>
public class ChangeLogInterceptor : SaveChangesInterceptor
{
    private readonly AuditDbContext _auditDbContext;
    private readonly IAuditConfigurationService _configService;
    private readonly AuditMetadataSerializer _metadataSerializer;
    private readonly ILogger<ChangeLogInterceptor>? _logger;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaReady;

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
        {
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
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null && ShouldAudit(eventData.Context))
        {
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
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Проверяет, нужно ли аудировать данный контекст
    /// </summary>
    private bool ShouldAudit(DbContext context)
    {
        // Не аудируем сам AuditDbContext, чтобы избежать бесконечной рекурсии
        return context is not AuditDbContext;
    }

    /// <summary>
    /// Захватывает изменения и записывает метаданные в audit_log (синхронная версия)
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

        var auditLog = new AuditLog
        {
            TransactionId = transactionId,
            Payload = payload,
            CreatedAt = DateTime.UtcNow
        };

        _auditDbContext.AuditLogs.Add(auditLog);
        _auditDbContext.SaveChanges();

        _logger?.LogInformation(
            "Audit metadata captured. TransactionId: {TransactionId}, Entities changed: {EntityCount}",
            transactionId,
            entries.Count);
    }

    /// <summary>
    /// Захватывает изменения и записывает метаданные в audit_log (асинхронная версия)
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

        var auditLog = new AuditLog
        {
            TransactionId = transactionId,
            Payload = payload,
            CreatedAt = DateTime.UtcNow
        };

        await _auditDbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
        await _auditDbContext.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation(
            "Audit metadata captured. TransactionId: {TransactionId}, Entities changed: {EntityCount}",
            transactionId,
            entries.Count);
    }

    /// <summary>
    /// Получает отслеживаемые записи, которые нужно аудировать
    /// </summary>
    private List<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> GetTrackedEntries(DbContext context)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added ||
                       e.State == EntityState.Modified ||
                       e.State == EntityState.Deleted)
            .ToList();

        // Фильтруем по конфигурации
        var auditableEntries = new List<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry>();

        foreach (var entry in entries)
        {
            var entityName = entry.Entity.GetType().Name;

            // Проверяем включено ли логирование для этой сущности
            if (_configService.IsEntityEnabled(entityName))
            {
                auditableEntries.Add(entry);
                _logger?.LogDebug(
                    "Entity {EntityName} with state {State} will be audited",
                    entityName,
                    entry.State);
            }
            else
            {
                _logger?.LogTrace(
                    "Entity {EntityName} is excluded from audit by configuration",
                    entityName);
            }
        }

        return auditableEntries;
    }

    /// <summary>
    /// Генерирует уникальный ID транзакции
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
            await _auditDbContext.Database.MigrateAsync(cancellationToken);
            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }
}
