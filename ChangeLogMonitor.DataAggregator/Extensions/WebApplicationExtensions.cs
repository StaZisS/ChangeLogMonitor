using ChangeLogMonitor.DataAggregator.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ChangeLogMonitor.DataAggregator.Extensions;

public static class WebApplicationExtensions
{
    public static IEndpointRouteBuilder MapDataAggregatorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/aggregator/healthz", ([FromServices] ITxAggregatorHealth health) =>
            {
                var snapshot = health.GetSnapshot();
                return Results.Json(new
                {
                    status = health.State,
                    running = health.IsRunning,
                    ready = health.IsReady,
                    metrics = snapshot
                });
            })
            .WithName("AggregatorHealth")
            .WithTags("Health");

        return app;
    }
}
