using ChangeLogMonitor.Api.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace ChangeLogMonitor.Api.Extensions;

public static class WebApplicationExtensions
{
    public static IEndpointRouteBuilder MapChangeLogMonitorApi(this IEndpointRouteBuilder app)
    {
        app.MapHealthEndpoints();
        app.MapDiffRawEndpoints();
        app.MapDiffFormattedEndpoints();
        app.MapDebugEndpoints();

        return app;
    }
}