using ChangeLogMonitor.Api.Extensions;
using ChangeLogMonitor.Embedded.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLogMonitor.Embedded.Extensions;

/// <summary>
///     Extension methods for configuring ChangeLogMonitor middleware pipeline.
/// </summary>
public static class WebApplicationExtensions
{
    public static IEndpointRouteBuilder UseChangeLogMonitor(this IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetService<EmbeddedChangeLogOptions>();

        if (options?.EnableApi == true)
        {
            var basePath = options.ApiBasePath.TrimEnd('/');

            if (string.IsNullOrEmpty(basePath) || basePath == "/")
            {
                app.MapChangeLogMonitorApi();
            }
            else
            {
                var group = app.MapGroup(basePath);
                group.MapChangeLogMonitorApi();
            }
        }

        return app;
    }
}