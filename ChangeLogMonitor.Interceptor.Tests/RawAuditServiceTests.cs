using Auditmeta.Raw;
using ChangeLogMonitor.Interceptor.Extensions;
using ChangeLogMonitor.Interceptor.Services;
using ChangeLogMonitor.Interceptor.Tests.Infrastructure;
using ChangeLogMonitor.Interceptor.Tests.TestEntities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ChangeLogMonitor.Interceptor.Tests;

[Collection("IntegrationTests")]
public class RawAuditServiceTests : IntegrationTestBase
{
    public RawAuditServiceTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    private RawAuditService CreateRawAuditService()
    {
        var auditDbContext = CreateAuditDbContext();
        var serializer = new AuditMetadataSerializer(MetadataProvider);
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<RawAuditService>();

        return new RawAuditService(auditDbContext, serializer, logger);
    }

    private TestAppDbContext CreateAppDbContextWithoutInterceptor()
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new TestAppDbContext(options);
    }

    [Fact]
    public async Task RecordRawOperationAsync_ShouldCreateAuditLog()
    {
        MetadataProvider.UserId = "raw-user-123";
        MetadataProvider.UserName = "Raw Test User";

        await using var context = CreateAppDbContextWithoutInterceptor();
        var rawAuditService = CreateRawAuditService();

        var user = new User
        {
            Name = "Test",
            Email = "test@test.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await CleanAuditLogAsync();

        await using var transaction = await context.Database.BeginTransactionAsync();

        var affected = await context.Database.ExecuteSqlRawAsync(
            "UPDATE users SET \"IsActive\" = false WHERE \"Id\" = {0}",
            user.Id);

        await rawAuditService.RecordRawOperationAsync(
            context,
            "users",
            affected,
            "Bulk deactivation");

        await transaction.CommitAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(1);

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);
        envelope.Actor.UserId.Should().Be("raw-user-123");
        envelope.Actor.UserName.Should().Be("Raw Test User");
        envelope.Bulk.Should().NotBeNull();
        envelope.Bulk.IsBulk.Should().BeTrue();
        envelope.Bulk.Target.Should().Be("users");
        envelope.Bulk.AffectedCount.Should().Be((uint)affected);
        envelope.Hints.Should().Contain(h => h.Key == "_reason" && h.Value == "Bulk deactivation");
    }

    [Fact]
    public async Task RecordRawOperation_ShouldIncludeHints()
    {
        await using var context = CreateAppDbContextWithoutInterceptor();
        var rawAuditService = CreateRawAuditService();

        var hints = new Dictionary<string, string>
        {
            { "batch_id", "batch-456" },
            { "source", "migration-script" }
        };

        await using var transaction = await context.Database.BeginTransactionAsync();

        await rawAuditService.RecordRawOperationAsync(
            context,
            "orders",
            10,
            "Cleanup old orders",
            hints);

        await transaction.CommitAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);

        envelope.Hints.Should().Contain(h => h.Key == "batch_id" && h.Value == "batch-456");
        envelope.Hints.Should().Contain(h => h.Key == "source" && h.Value == "migration-script");
        envelope.Hints.Should().Contain(h => h.Key == "_reason" && h.Value == "Cleanup old orders");
    }

    [Fact]
    public async Task RecordRawOperationFromScopeAsync_ShouldUseAuditScopeData()
    {
        MetadataProvider.UserId = "scope-user";
        MetadataProvider.UserName = "Scope User";

        await using var context = CreateAppDbContextWithoutInterceptor();
        var rawAuditService = CreateRawAuditService();

        using (AuditScope.Begin()
                   .SetTargetEntity("session_caches")
                   .SetReason("Cleanup expired sessions")
                   .AddHint("cleanup_type", "expired"))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            var affected = await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM session_caches WHERE 1=0");

            await rawAuditService.RecordRawOperationFromScopeAsync(context, affected);

            await transaction.CommitAsync();
        }

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(1);

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);
        envelope.Bulk.Target.Should().Be("session_caches");
        envelope.Hints.Should().Contain(h => h.Key == "_reason" && h.Value == "Cleanup expired sessions");
        envelope.Hints.Should().Contain(h => h.Key == "cleanup_type" && h.Value == "expired");
    }

    [Fact]
    public async Task RecordRawOperationFromScopeAsync_ShouldThrowWithoutScope()
    {
        await using var context = CreateAppDbContextWithoutInterceptor();
        var rawAuditService = CreateRawAuditService();

        var act = async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await rawAuditService.RecordRawOperationFromScopeAsync(context, 5);
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No active AuditScope*");
    }

    [Fact]
    public async Task RecordRawOperationFromScopeAsync_ShouldThrowWithoutTargetEntity()
    {
        await using var context = CreateAppDbContextWithoutInterceptor();
        var rawAuditService = CreateRawAuditService();

        var act = async () =>
        {
            using (AuditScope.Begin().SetReason("Test"))
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                await rawAuditService.RecordRawOperationFromScopeAsync(context, 5);
            }
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TargetEntity is not set*");
    }

    [Fact]
    public async Task ExecuteSqlRawWithAuditAsync_ShouldExecuteAndRecordAudit()
    {
        MetadataProvider.UserId = "extension-user";
        MetadataProvider.UserName = "Extension User";

        await using var context = CreateAppDbContextWithoutInterceptor();
        var rawAuditService = CreateRawAuditService();

        var user = new User
        {
            Name = "To Update",
            Email = "update@test.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        await CleanAuditLogAsync();

        await using var transaction = await context.Database.BeginTransactionAsync();

        var affected = await context.ExecuteSqlRawWithAuditAsync(
            rawAuditService,
            "users",
            "UPDATE users SET \"IsActive\" = false WHERE \"Id\" = {0}",
            new object[] { user.Id },
            "Deactivate user via extension");

        await transaction.CommitAsync();

        affected.Should().Be(1);

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(1);

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);
        envelope.Bulk.Target.Should().Be("users");
        envelope.Bulk.AffectedCount.Should().Be(1);
        envelope.Actor.UserId.Should().Be("extension-user");
    }

    [Fact]
    public async Task ExecuteSqlInterpolatedWithAuditAsync_ShouldExecuteAndRecordAudit()
    {
        await using var context = CreateAppDbContextWithoutInterceptor();
        var rawAuditService = CreateRawAuditService();

        var user = new User
        {
            Name = "Interpolated Test",
            Email = "interpolated@test.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        await CleanAuditLogAsync();

        var userId = user.Id;

        await using var transaction = await context.Database.BeginTransactionAsync();

        var affected = await context.ExecuteSqlInterpolatedWithAuditAsync(
            rawAuditService,
            "users",
            $"UPDATE users SET \"IsActive\" = false WHERE \"Id\" = {userId}",
            "Interpolated update");

        await transaction.CommitAsync();

        affected.Should().Be(1);

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(1);

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);
        envelope.Bulk.Target.Should().Be("users");
    }

    [Fact]
    public async Task ExecuteSqlRawWithScopeAuditAsync_ShouldUseAuditScope()
    {
        await using var context = CreateAppDbContextWithoutInterceptor();
        var rawAuditService = CreateRawAuditService();

        using (AuditScope.Begin()
                   .SetTargetEntity("users")
                   .SetReason("Scope-based update")
                   .AddHint("operation", "bulk_update"))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            var affected = await context.ExecuteSqlRawWithScopeAuditAsync(
                rawAuditService,
                "UPDATE users SET \"IsActive\" = true WHERE 1=0");

            await transaction.CommitAsync();
        }

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(1);

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);
        envelope.Bulk.Target.Should().Be("users");
        envelope.Hints.Should().Contain(h => h.Key == "_reason" && h.Value == "Scope-based update");
        envelope.Hints.Should().Contain(h => h.Key == "operation" && h.Value == "bulk_update");
    }

    [Fact]
    public async Task NestedScopes_ShouldInheritAndOverrideCorrectly()
    {
        await using var context = CreateAppDbContextWithoutInterceptor();
        var rawAuditService = CreateRawAuditService();

        using (AuditScope.Begin()
                   .SetTargetEntity("outer_table")
                   .AddHint("level", "outer"))
        {
            using (AuditScope.Begin()
                       .SetTargetEntity("inner_table")
                       .AddHint("level", "inner"))
            {
                await using var transaction = await context.Database.BeginTransactionAsync();

                await rawAuditService.RecordRawOperationFromScopeAsync(context, 5);

                await transaction.CommitAsync();
            }
        }

        var auditLogs = await GetAllAuditLogsAsync();
        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);

        envelope.Bulk.Target.Should().Be("inner_table");
        envelope.Hints.Should().Contain(h => h.Key == "level" && h.Value == "inner");
    }

    [Fact]
    public async Task RequestContext_ShouldBeIncludedInBulkOperation()
    {
        MetadataProvider.RequestId = "bulk-req-123";
        MetadataProvider.ServiceName = "BulkService";
        MetadataProvider.ClientIp = "10.0.0.1";
        MetadataProvider.UserAgent = "BulkClient/1.0";

        await using var context = CreateAppDbContextWithoutInterceptor();
        var rawAuditService = CreateRawAuditService();

        await using var transaction = await context.Database.BeginTransactionAsync();

        await rawAuditService.RecordRawOperationAsync(
            context,
            "orders",
            100);

        await transaction.CommitAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLogs[0].Payload);

        envelope.Request.Should().NotBeNull();
        envelope.Request.RequestId.Should().Be("bulk-req-123");
        envelope.Request.ServiceName.Should().Be("BulkService");
        envelope.Request.ClientIp.Should().Be("10.0.0.1");
        envelope.Request.UserAgent.Should().Be("BulkClient/1.0");
    }
}