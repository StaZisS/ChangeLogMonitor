using Auditmeta.Raw;
using ChangeLogMonitor.Interceptor.Tests.Infrastructure;
using ChangeLogMonitor.Interceptor.Tests.TestEntities;
using FluentAssertions;
using Xunit;

namespace ChangeLogMonitor.Interceptor.Tests;

[Collection("IntegrationTests")]
public class ChangeLogInterceptorPayloadTests : IntegrationTestBase
{
    private const string WhitelistConfigPath = "TestConfigs/whitelist-config.yaml";

    public ChangeLogInterceptorPayloadTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Should_ContainCorrectUserInfo_InPayload()
    {
        MetadataProvider.UserId = "user-123";
        MetadataProvider.UserName = "John Smith";

        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Test User",
            Email = "test@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(1);

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);

        envelope.Should().NotBeNull();
        envelope.Actor.Should().NotBeNull();
        envelope.Actor.UserId.Should().Be("user-123");
        envelope.Actor.UserName.Should().Be("John Smith");
    }

    [Fact]
    public async Task Should_ContainTransactionId_InPayload()
    {
        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Test User",
            Email = "test@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        var auditLog = auditLogs.First();
        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLog.Payload);

        envelope.TransactionId.Should().NotBeNullOrEmpty();
        envelope.TransactionId.Should().Be(auditLog.TransactionId,
            "payload transaction_id should match audit_log.transaction_id");
    }

    [Fact]
    public async Task Should_ContainCreatedAtTimestamp_InPayload()
    {
        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var beforeSave = DateTimeOffset.UtcNow;

        var user = new User
        {
            Name = "Test User",
            Email = "test@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var afterSave = DateTimeOffset.UtcNow;

        var auditLogs = await GetAllAuditLogsAsync();
        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);

        envelope.CreatedAtUtcMs.Should().BeGreaterOrEqualTo(beforeSave.ToUnixTimeMilliseconds());
        envelope.CreatedAtUtcMs.Should().BeLessOrEqualTo(afterSave.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task Should_ContainRequestContext_WhenProvided()
    {
        MetadataProvider.RequestId = "req-test-789";
        MetadataProvider.ServiceName = "TestService";
        MetadataProvider.ClientIp = "192.168.1.100";
        MetadataProvider.UserAgent = "TestAgent/2.0";

        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Test User",
            Email = "test@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);

        envelope.Request.Should().NotBeNull();
        envelope.Request.RequestId.Should().Be("req-test-789");
        envelope.Request.ServiceName.Should().Be("TestService");
        envelope.Request.ClientIp.Should().Be("192.168.1.100");
        envelope.Request.UserAgent.Should().Be("TestAgent/2.0");
    }

    [Fact]
    public async Task Should_ContainHints_WhenProvided()
    {
        MetadataProvider.Hints = new Dictionary<string, string>
        {
            { "action", "user-registration" },
            { "source", "web-app" },
            { "version", "1.0" }
        };

        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Test User",
            Email = "test@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);

        envelope.Hints.Should().HaveCount(3);
        envelope.Hints.Should().Contain(h => h.Key == "action" && h.Value == "user-registration");
        envelope.Hints.Should().Contain(h => h.Key == "source" && h.Value == "web-app");
        envelope.Hints.Should().Contain(h => h.Key == "version" && h.Value == "1.0");
    }

    [Fact]
    public async Task Should_NotContainRequestContext_WhenNotProvided()
    {
        MetadataProvider.RequestId = null;
        MetadataProvider.ServiceName = null;
        MetadataProvider.ClientIp = null;
        MetadataProvider.UserAgent = null;

        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Test User",
            Email = "test@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);

        if (envelope.Request != null) envelope.Request.RequestId.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Should_NotContainHints_WhenNotProvided()
    {
        MetadataProvider.Hints = null;

        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Test User",
            Email = "test@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);

        envelope.Hints.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_HaveSameTransactionId_ForMultipleEntitiesInSameTransaction()
    {
        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Test User",
            Email = "test@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var order = new Order
        {
            OrderNumber = "ORD-001",
            TotalAmount = 100.00m,
            User = user,
            OrderDate = DateTime.UtcNow
        };

        context.Users.Add(user);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(1, "one transaction should create one audit log");

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);
        envelope.TransactionId.Should().Be(auditLogs[0].TransactionId);
    }

    [Fact]
    public async Task Should_BeDeserializable_FromPayload()
    {
        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Serialization Test",
            Email = "serialize@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        var payload = auditLogs[0].Payload;

        Action deserializeAction = () => AuditMetaEnvelope.Parser.ParseFrom(payload);
        deserializeAction.Should().NotThrow("payload should be valid protobuf");

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(payload);
        envelope.Should().NotBeNull();
        envelope.Actor.Should().NotBeNull();
        envelope.TransactionId.Should().NotBeNullOrEmpty();
    }
}