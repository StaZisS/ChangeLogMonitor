using ChangeLogMonitor.Api.Services;
using ChangeLogMonitor.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLogMonitor.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChangeLogMonitorApi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();

        return services;
    }
}