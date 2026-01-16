using ChangeLogMonitor.Api.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace ChangeLogMonitor.Api.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Maps all ChangeLogMonitor API endpoints.
    /// Includes: Health, Diffs (raw and formatted), Debug endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapChangeLogMonitorApi(this IEndpointRouteBuilder app)
    {
        app.MapHealthEndpoints();
        app.MapDiffRawEndpoints();
        app.MapDiffFormattedEndpoints();
        app.MapDebugEndpoints();

        return app;
    }
}
