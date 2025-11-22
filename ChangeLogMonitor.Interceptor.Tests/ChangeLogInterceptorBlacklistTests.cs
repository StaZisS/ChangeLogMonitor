using ChangeLogMonitor.Interceptor.Tests.Infrastructure;
using ChangeLogMonitor.Interceptor.Tests.TestEntities;
using FluentAssertions;
using Xunit;

namespace ChangeLogMonitor.Interceptor.Tests;

/// <summary>
///     Интеграционные тесты для ChangeLogInterceptor в режиме blacklist
/// </summary>
[Collection("IntegrationTests")]
public class ChangeLogInterceptorBlacklistTests : IntegrationTestBase
{
    private const string BlacklistConfigPath = "TestConfigs/blacklist-config.yaml";

    public ChangeLogInterceptorBlacklistTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Should_CreateAuditLog_When_NotBlacklistedEntityIsCreated()
    {
        // Arrange
        await using var context = CreateAppDbContext(BlacklistConfigPath);

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
        auditLogCount.Should().Be(1, "User is NOT in blacklist, so should be audited");
    }

    [Fact]
    public async Task Should_CreateAuditLog_When_NotBlacklistedEntityIsUpdated()
    {
        // Arrange
        await using var context = CreateAppDbContext(BlacklistConfigPath);

        var user = new User
        {
            Name = "Order Owner",
            Email = "owner@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var order = new Order
        {
            OrderNumber = "ORD-001",
            TotalAmount = 100.00m,
            UserId = user.Id,
            User = user,
            OrderDate = DateTime.UtcNow
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        await CleanAuditLogAsync(); // Очищаем audit_log после создания

        // Act - обновляем заказ
        order.TotalAmount = 150.00m;
        await context.SaveChangesAsync();

        // Assert
        var auditLogCount = await GetAuditLogCountAsync();
        auditLogCount.Should().Be(1, "Order is NOT in blacklist, so should be audited");
    }

    [Fact]
    public async Task Should_NotCreateAuditLog_When_BlacklistedEntityIsCreated()
    {
        // Arrange
        await using var context = CreateAppDbContext(BlacklistConfigPath);

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
        auditLogCount.Should().Be(0, "SessionCache is explicitly disabled in blacklist config");
    }

    [Fact]
    public async Task Should_NotCreateAuditLog_When_BlacklistedEntityIsUpdated()
    {
        // Arrange
        await using var context = CreateAppDbContext(BlacklistConfigPath);

        var sessionCache = new SessionCache
        {
            SessionId = "session-456",
            Data = "initial data",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        context.SessionCaches.Add(sessionCache);
        await context.SaveChangesAsync();

        // Очищаем audit_log (хотя там и так должно быть пусто)
        await CleanAuditLogAsync();

        // Act - обновляем SessionCache
        sessionCache.Data = "updated data";
        await context.SaveChangesAsync();

        // Assert
        var auditLogCount = await GetAuditLogCountAsync();
        auditLogCount.Should().Be(0, "SessionCache updates should NOT be audited");
    }

    [Fact]
    public async Task Should_NotCreateAuditLog_When_BlacklistedEntityIsDeleted()
    {
        // Arrange
        await using var context = CreateAppDbContext(BlacklistConfigPath);

        var sessionCache = new SessionCache
        {
            SessionId = "session-789",
            Data = "to be deleted",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        context.SessionCaches.Add(sessionCache);
        await context.SaveChangesAsync();

        await CleanAuditLogAsync();

        // Act - удаляем SessionCache
        context.SessionCaches.Remove(sessionCache);
        await context.SaveChangesAsync();

        // Assert
        var auditLogCount = await GetAuditLogCountAsync();
        auditLogCount.Should().Be(0, "SessionCache deletion should NOT be audited");
    }

    [Fact]
    public async Task Should_CreateAuditLog_OnlyForNonBlacklisted_When_MixedEntitiesAreSaved()
    {
        // Arrange
        await using var context = CreateAppDbContext(BlacklistConfigPath);

        var user = new User
        {
            Name = "Alice Brown",
            Email = "alice@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var sessionCache = new SessionCache
        {
            SessionId = "session-mixed",
            Data = "cached data",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act - сохраняем обе сущности в одной транзакции
        context.Users.Add(user);
        context.SessionCaches.Add(sessionCache);
        await context.SaveChangesAsync();

        // Assert
        var auditLogCount = await GetAuditLogCountAsync();
        auditLogCount.Should().Be(1, "only User should be audited, SessionCache is blacklisted");
    }

    [Fact]
    public async Task Should_NotCreateAuditLog_When_OnlyBlacklistedEntitiesAreSaved()
    {
        // Arrange
        await using var context = CreateAppDbContext(BlacklistConfigPath);

        var sessionCache1 = new SessionCache
        {
            SessionId = "session-001",
            Data = "data 1",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        var sessionCache2 = new SessionCache
        {
            SessionId = "session-002",
            Data = "data 2",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act
        context.SessionCaches.Add(sessionCache1);
        context.SessionCaches.Add(sessionCache2);
        await context.SaveChangesAsync();

        // Assert
        var auditLogCount = await GetAuditLogCountAsync();
        auditLogCount.Should().Be(0, "both entities are blacklisted, no audit log should be created");
    }
}