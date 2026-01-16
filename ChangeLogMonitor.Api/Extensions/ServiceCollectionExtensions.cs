using ChangeLogMonitor.Api.Services;
using ChangeLogMonitor.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLogMonitor.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ChangeLogMonitor API services to the service collection.
    /// This includes HttpContextAccessor and CurrentUserService for HTTP-based user identification.
    /// </summary>
    public static IServiceCollection AddChangeLogMonitorApi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();

        return services;
    }
}
