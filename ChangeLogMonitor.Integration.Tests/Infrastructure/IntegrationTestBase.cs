using ChangeLogMonitor.Configuration.Providers;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Core.Services;
using ChangeLogMonitor.Integration.Tests.Helpers;
using ChangeLogMonitor.Integration.Tests.TestEntities;
using ChangeLogMonitor.Interceptor.Interceptors;
using ChangeLogMonitor.Interceptor.Models;
using ChangeLogMonitor.Interceptor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace ChangeLogMonitor.Integration.Tests.Infrastructure;

/// <summary>
///     Базовый класс для интеграционных тестов
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;

    protected IntegrationTestBase(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        MetadataProvider = new TestAuditMetadataProvider();
    }

    protected string ConnectionString => _fixture.ConnectionString;
    protected TestAuditMetadataProvider MetadataProvider { get; }

    public async Task InitializeAsync()
    {
        await _fixture.WaitForSchemaAsync();
        await CleanDatabaseAsync();
        MetadataProvider.Reset();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Создает AuditDbContext для работы с audit_log
    /// </summary>
    protected AuditDbContext CreateAuditDbContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AuditDbContext(options);
    }

    /// <summary>
    ///     Создает TestAppDbContext с интерцептором
    /// </summary>
    protected TestAppDbContext CreateAppDbContext(string configFilePath)
    {
        var auditDbContext = CreateAuditDbContext();
        var policyProvider = new YamlAuditPolicyProvider(configFilePath);
        var configService = new AuditConfigurationService(policyProvider);
        var serializer = new AuditMetadataSerializer(MetadataProvider);
        var enumLabelProvider = new AttributeEnumLabelProvider();
        var enumExtractor = new EnumMetadataExtractor(enumLabelProvider);
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ChangeLogInterceptor>();

        var interceptor = new ChangeLogInterceptor(
            auditDbContext,
            configService,
            serializer,
            enumExtractor,
            logger);

        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new TestAppDbContext(options);
    }

    /// <summary>
    ///     Создает AuditConfigurationService для форматирования
    /// </summary>
    protected IAuditConfigurationService CreateConfigurationService(string configFilePath)
    {
        var policyProvider = new YamlAuditPolicyProvider(configFilePath);
        return new AuditConfigurationService(policyProvider);
    }

    /// <summary>
    ///     Очищает все данные из таблиц
    /// </summary>
    protected async Task CleanDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            TRUNCATE TABLE order_items RESTART IDENTITY CASCADE;
            TRUNCATE TABLE orders RESTART IDENTITY CASCADE;
            TRUNCATE TABLE products RESTART IDENTITY CASCADE;
            TRUNCATE TABLE customers RESTART IDENTITY CASCADE;
            TRUNCATE TABLE audit_log RESTART IDENTITY CASCADE;
        ";

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    ///     Получает все записи из audit_log
    /// </summary>
    protected async Task<List<AuditLog>> GetAllAuditLogsAsync()
    {
        await using var context = CreateAuditDbContext();
        return await context.AuditLogs.OrderBy(a => a.CreatedAt).ToListAsync();
    }

    /// <summary>
    ///     Получает количество записей в audit_log
    /// </summary>
    protected async Task<int> GetAuditLogCountAsync()
    {
        await using var context = CreateAuditDbContext();
        return await context.AuditLogs.CountAsync();
    }
}
