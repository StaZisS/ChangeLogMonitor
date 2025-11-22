using ChangeLogMonitor.Interceptor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChangeLogMonitor.Interceptor.Services;

internal sealed class AuditDbMigrationHostedService : IHostedService
{
    private readonly ILogger<AuditDbMigrationHostedService>? _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public AuditDbMigrationHostedService(IServiceScopeFactory scopeFactory,
        ILogger<AuditDbMigrationHostedService>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            _logger?.LogInformation("Applied audit schema migrations.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to apply audit schema migrations.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}