using ChangeLogMonitor.Interceptor.Tests.Infrastructure;
using ChangeLogMonitor.Interceptor.Tests.TestEntities;
using FluentAssertions;
using Xunit;

namespace ChangeLogMonitor.Interceptor.Tests;

[Collection("IntegrationTests")]
public class ChangeLogInterceptorTransactionTests : IntegrationTestBase
{
    private const string WhitelistConfigPath = "TestConfigs/whitelist-config.yaml";

    public ChangeLogInterceptorTransactionTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Should_WriteAuditLogInSameTransaction()
    {
        await using var context = CreateAppDbContext(WhitelistConfigPath);

        var user = new User
        {
            Name = "Tx User",
            Email = "tx@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(1, "audit_log must be written in the same transaction as business changes");
    }
}