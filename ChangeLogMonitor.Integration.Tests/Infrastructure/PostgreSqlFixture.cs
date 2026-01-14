using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace ChangeLogMonitor.Integration.Tests.Infrastructure;

/// <summary>
///     Fixture для PostgreSQL контейнера.
///     Создаёт один контейнер для всех тестов в коллекции
///     и создает схему БД один раз перед всеми тестами.
/// </summary>
public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly SemaphoreSlim _schemaReadySemaphore = new(0, 1);

    public PostgreSqlContainer PostgresContainer { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        PostgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await PostgresContainer.StartAsync();
        ConnectionString = PostgresContainer.GetConnectionString();

        await CreateDatabaseSchemaAsync();
        _schemaReadySemaphore.Release();
    }

    public async Task DisposeAsync()
    {
        await PostgresContainer.DisposeAsync();
    }

    public async Task WaitForSchemaAsync()
    {
        await _schemaReadySemaphore.WaitAsync();
        _schemaReadySemaphore.Release();
    }

    private async Task CreateDatabaseSchemaAsync()
    {
        var schemaOptions = new DbContextOptionsBuilder<TestSchemaDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var schemaContext = new TestSchemaDbContext(schemaOptions);
        await schemaContext.Database.EnsureCreatedAsync();
    }
}
