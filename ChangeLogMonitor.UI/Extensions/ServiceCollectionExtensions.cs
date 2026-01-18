using ChangeLogMonitor.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLogMonitor.UI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuditLogUI(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogViewService, AuditLogViewService>();
        return services;
    }
}