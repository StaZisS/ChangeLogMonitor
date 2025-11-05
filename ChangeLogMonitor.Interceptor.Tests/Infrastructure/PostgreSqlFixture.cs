using Microsoft.EntityFrameworkCore;
using System.Threading;
using Testcontainers.PostgreSql;
using Xunit;

namespace ChangeLogMonitor.Interceptor.Tests.Infrastructure;

/// <summary>
/// Fixture для PostgreSQL контейнера.
/// Создаёт один контейнер для всех тестов в коллекции
/// и создает схему БД один раз перед всеми тестами.
/// </summary>
public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly SemaphoreSlim _schemaReadySemaphore = new SemaphoreSlim(0, 1);

    public PostgreSqlContainer PostgresContainer { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;

    /// <summary>
    /// Ожидает завершения создания схемы БД
    /// </summary>
    public async Task WaitForSchemaAsync()
    {
        await _schemaReadySemaphore.WaitAsync();
        _schemaReadySemaphore.Release(); // Сразу освобождаем для других потоков
    }

    public async Task InitializeAsync()
    {
        // Запускаем PostgreSQL контейнер один раз для всех тестов
        PostgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await PostgresContainer.StartAsync();
        ConnectionString = PostgresContainer.GetConnectionString();

        // Создаем схему БД один раз для всех тестов
        await CreateDatabaseSchemaAsync();

        // Сигнализируем что схема готова
        _schemaReadySemaphore.Release();
    }

    public async Task DisposeAsync()
    {
        await PostgresContainer.DisposeAsync();
    }

    /// <summary>
    /// Создает схему БД для тестов
    /// </summary>
    private async Task CreateDatabaseSchemaAsync()
    {
        var schemaOptions = new DbContextOptionsBuilder<TestSchemaDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var schemaContext = new TestSchemaDbContext(schemaOptions);
        await schemaContext.Database.EnsureCreatedAsync();
    }
}
