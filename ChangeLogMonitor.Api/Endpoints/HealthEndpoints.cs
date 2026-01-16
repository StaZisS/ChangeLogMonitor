using ChangeLogMonitor.DataAggregator.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ChangeLogMonitor.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Combined health endpoint
        app.MapGet("/healthz", ([FromServices] ITxAggregatorHealth aggregatorHealth) =>
            {
                var aggregatorSnapshot = aggregatorHealth.GetSnapshot();
                return Results.Json(new
                {
                    status = "ok",
                    service = "changelog-monitor",
                    aggregator = new
                    {
                        status = aggregatorHealth.State,
                        running = aggregatorHealth.IsRunning,
                        ready = aggregatorHealth.IsReady,
                        metrics = aggregatorSnapshot
                    },
                    finalizer = new
                    {
                        status = "ok"
                    }
                });
            })
            .WithName("CombinedHealth")
            .WithTags("Health");

        // Aggregator health endpoint
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

        // Finalizer health endpoint
        app.MapGet("/finalizer/healthz", () => Results.Json(new
            {
                status = "ok",
                service = "finalizer"
            }))
            .WithName("FinalizerHealth")
            .WithTags("Health");

        return app;
    }
}
