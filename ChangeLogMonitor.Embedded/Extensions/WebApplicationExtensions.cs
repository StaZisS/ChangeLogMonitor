using ChangeLogMonitor.Api.Extensions;
using ChangeLogMonitor.Embedded.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLogMonitor.Embedded.Extensions;

/// <summary>
/// Extension methods for configuring ChangeLogMonitor middleware pipeline.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures ChangeLogMonitor middleware and endpoints.
    /// Maps API endpoints if EnableApi option is true.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder UseChangeLogMonitor(this IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetService<EmbeddedChangeLogOptions>();

        if (options?.EnableApi == true)
        {
            var basePath = options.ApiBasePath.TrimEnd('/');

            if (string.IsNullOrEmpty(basePath) || basePath == "/")
            {
                // Map at root
                app.MapChangeLogMonitorApi();
            }
            else
            {
                // Map with prefix
                var group = app.MapGroup(basePath);
                group.MapChangeLogMonitorApi();
            }
        }

        return app;
    }
}
