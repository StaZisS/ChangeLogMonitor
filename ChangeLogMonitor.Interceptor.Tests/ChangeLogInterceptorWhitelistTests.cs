using ChangeLogMonitor.Interceptor.Tests.Infrastructure;
using ChangeLogMonitor.Interceptor.Tests.TestEntities;
using FluentAssertions;
using Xunit;

namespace ChangeLogMonitor.Interceptor.Tests;

/// <summary>
/// Интеграционные тесты для ChangeLogInterceptor в режиме whitelist
/// </summary>
[Collection("IntegrationTests")]
public class ChangeLogInterceptorWhitelistTests : IntegrationTestBase
{
    private const string WhitelistConfigPath = "TestConfigs/whitelist-config.yaml";

    public ChangeLogInterceptorWhitelistTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Should_CreateAuditLog_When_AuditableEntityIsCreated()
    {
        // Arrange
        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "John Doe",
            Email = "john@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert
        var auditLogCount = await GetAuditLogCountAsync();
        auditLogCount.Should().Be(1, "one User entity was created and User is in whitelist");

        var auditLogs = await GetAllAuditLogsAsync();
        var auditLog = auditLogs.First();
        auditLog.TransactionId.Should().NotBeNullOrEmpty();
        auditLog.Payload.Should().NotBeNullOrEmpty();
        auditLog.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        auditLog.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task Should_CreateAuditLog_When_AuditableEntityIsUpdated()
    {
        // Arrange
        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Jane Doe",
            Email = "jane@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await CleanAuditLogAsync(); // Очищаем audit_log после создания

        // Act - обновляем пользователя
        user.Name = "Jane Smith";
        user.Email = "jane.smith@example.com";
        await context.SaveChangesAsync();

        // Assert
        var auditLogCount = await GetAuditLogCountAsync();
        auditLogCount.Should().Be(1, "one User entity was updated and User is in whitelist");
    }

    [Fact]
    public async Task Should_CreateAuditLog_When_AuditableEntityIsDeleted()
    {
        // Arrange
        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Bob Wilson",
            Email = "bob@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await CleanAuditLogAsync(); // Очищаем audit_log после создания

        // Act - удаляем пользователя
        context.Users.Remove(user);
        await context.SaveChangesAsync();

        // Assert
        var auditLogCount = await GetAuditLogCountAsync();
        auditLogCount.Should().Be(1, "one User entity was deleted and User is in whitelist");
    }

    [Fact]
    public async Task Should_NotCreateAuditLog_When_NonAuditableEntityIsCreated()
    {
        // Arrange
        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var sessionCache = new SessionCache
        {
            SessionId = "session-123",
            Data = "some data",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act
        context.SessionCaches.Add(sessionCache);
        await context.SaveChangesAsync();

        // Assert
        var auditLogCount = await GetAuditLogCountAsync();
        auditLogCount.Should().Be(0, "SessionCache is NOT in whitelist, so should not be audited");
    }

    [Fact]
    public async Task Should_CreateAuditLog_ForEachAuditableEntity_When_MultipleEntitiesAreCreated()
    {
        // Arrange
        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Alice Brown",
            Email = "alice@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(); // Сохраняем user, чтобы получить Id

        var order = new Order
        {
            OrderNumber = "ORD-001",
            TotalAmount = 99.99m,
            UserId = user.Id,
            OrderDate = DateTime.UtcNow
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync(); // Сохраняем order

        // Assert
        var auditLogCount = await GetAuditLogCountAsync();
        auditLogCount.Should().Be(2, "both User and Order are in whitelist");
    }

    [Fact]
    public async Task Should_CreateOnlyOneAuditLog_When_MultipleAuditableEntitiesInSingleTransaction()
    {
        // Arrange
        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Charlie Davis",
            Email = "charlie@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var order = new Order
        {
            OrderNumber = "ORD-002",
            TotalAmount = 199.99m,
            User = user,
            OrderDate = DateTime.UtcNow
        };

        // Act - добавляем обе сущности в одной транзакции
        context.Users.Add(user);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Assert
        var auditLogCount = await GetAuditLogCountAsync();
        auditLogCount.Should().Be(1, "one transaction captured metadata for multiple entities");

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.First().TransactionId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_CreateSeparateAuditLogs_When_EntitiesSavedInSeparateTransactions()
    {
        // Arrange
        // Act - первая транзакция
        await using (var context1 = CreateAppDbContext(WhitelistConfigPath))
        {
            var user1 = new User
            {
                Name = "User One",
                Email = "user1@example.com",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context1.Users.Add(user1);
            await context1.SaveChangesAsync();
        }

        // Act - вторая транзакция
        await using (var context2 = CreateAppDbContext(WhitelistConfigPath))
        {
            var user2 = new User
            {
                Name = "User Two",
                Email = "user2@example.com",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context2.Users.Add(user2);
            await context2.SaveChangesAsync();
        }

        // Assert
        var auditLogCount = await GetAuditLogCountAsync();
        auditLogCount.Should().Be(2, "two separate transactions should create two audit logs");

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs[0].TransactionId.Should().NotBe(auditLogs[1].TransactionId,
            "different transactions should have different transaction IDs");
    }
}
