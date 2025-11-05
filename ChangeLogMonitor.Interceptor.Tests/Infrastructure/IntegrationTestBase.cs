using ChangeLogMonitor.Configuration.Providers;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Interceptor.Interceptors;
using ChangeLogMonitor.Interceptor.Models;
using ChangeLogMonitor.Interceptor.Services;
using ChangeLogMonitor.Interceptor.Tests.Helpers;
using ChangeLogMonitor.Interceptor.Tests.TestEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace ChangeLogMonitor.Interceptor.Tests.Infrastructure;

/// <summary>
/// Базовый класс для интеграционных тестов с PostgreSQL через Testcontainers
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;

    protected string ConnectionString => _fixture.ConnectionString;
    protected TestAuditMetadataProvider MetadataProvider { get; private set; } = null!;

    protected IntegrationTestBase(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        MetadataProvider = new TestAuditMetadataProvider();
    }

    public async Task InitializeAsync()
    {
        // Ждём пока PostgreSqlFixture создаст схему БД
        await _fixture.WaitForSchemaAsync();

        // Очищаем данные перед каждым тестом
        await CleanDatabaseAsync();

        // Сбрасываем MetadataProvider к значениям по умолчанию
        MetadataProvider.Reset();
    }

    public Task DisposeAsync()
    {
        // Cleanup выполняется в InitializeAsync следующего теста
        return Task.CompletedTask;
    }

    /// <summary>
    /// Создает AuditDbContext
    /// </summary>
    protected AuditDbContext CreateAuditDbContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AuditDbContext(options);
    }

    /// <summary>
    /// Создает TestAppDbContext с интерцептором
    /// </summary>
    protected TestAppDbContext CreateAppDbContext(string configFilePath)
    {
        // Создаем сервисы
        var auditDbContext = CreateAuditDbContext();
        var policyProvider = new YamlAuditPolicyProvider(configFilePath);
        var configService = new AuditConfigurationService(policyProvider);
        var serializer = new AuditMetadataSerializer(MetadataProvider);
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ChangeLogInterceptor>();

        var interceptor = new ChangeLogInterceptor(
            auditDbContext,
            configService,
            serializer,
            logger);

        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new TestAppDbContext(options);
    }

    /// <summary>
    /// Очищает все данные из таблиц
    /// </summary>
    protected async Task CleanDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();

        // Очищаем таблицы в правильном порядке (сначала зависимые)
        // TRUNCATE автоматически сбрасывает sequences (RESTART IDENTITY)
        // CASCADE удаляет связанные данные
        command.CommandText = @"
            TRUNCATE TABLE orders RESTART IDENTITY CASCADE;
            TRUNCATE TABLE session_caches RESTART IDENTITY CASCADE;
            TRUNCATE TABLE users RESTART IDENTITY CASCADE;
            TRUNCATE TABLE audit_log RESTART IDENTITY CASCADE;
        ";

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Очищает только таблицу audit_log, сохраняя остальные данные
    /// </summary>
    protected async Task CleanAuditLogAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "TRUNCATE TABLE audit_log RESTART IDENTITY;";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Получает количество записей в audit_log
    /// </summary>
    protected async Task<int> GetAuditLogCountAsync()
    {
        await using var context = CreateAuditDbContext();
        return await context.AuditLogs.CountAsync();
    }

    /// <summary>
    /// Получает все записи из audit_log
    /// </summary>
    protected async Task<List<AuditLog>> GetAllAuditLogsAsync()
    {
        await using var context = CreateAuditDbContext();
        return await context.AuditLogs.OrderBy(a => a.CreatedAt).ToListAsync();
    }
}
