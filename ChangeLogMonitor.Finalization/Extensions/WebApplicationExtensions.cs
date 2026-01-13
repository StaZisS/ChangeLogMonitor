using ChangeLogMonitor.Finalization.Models;
using ChangeLogMonitor.Finalization.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ChangeLogMonitor.Finalization.Extensions;

public static class WebApplicationExtensions
{
    public static IEndpointRouteBuilder MapFinalizationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/finalizer/healthz", () => Results.Json(new
            {
                status = "ok",
                service = "finalizer"
            }))
            .WithName("FinalizerHealth")
            .WithTags("Health");

        app.MapGet("/diffs/{tableName}/{entityId}", async Task<IResult> (
                string tableName,
                string entityId,
                [FromQuery] int? limit,
                [FromServices] IDiffService diffService,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(entityId))
                    return Results.BadRequest(new { message = "tableName and entityId are required." });

                var take = Math.Clamp(limit ?? 50, 1, 500);
                var response = await diffService.GetByEntityAsync(tableName, entityId, take, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetDiffsByEntity")
            .WithTags("Diffs");

        app.MapGet("/diffs/tx/{transactionId}", async Task<IResult> (
                string transactionId,
                [FromQuery] int? limit,
                [FromServices] IDiffService diffService,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(transactionId))
                    return Results.BadRequest(new { message = "transactionId is required." });

                var take = Math.Clamp(limit ?? 50, 1, 500);
                var response = await diffService.GetByTransactionAsync(transactionId, take, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetDiffsByTransaction")
            .WithTags("Diffs");

        app.MapGet("/diffs", async Task<IResult> (
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                [FromServices] IDiffService diffService,
                CancellationToken cancellationToken) =>
            {
                var take = Math.Clamp(limit ?? 50, 1, 500);
                var result = await diffService.GetAllAsync(cursor, take, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetAllDiffs")
            .WithTags("Diffs");

        app.MapGet("/diffs/filter", async Task<IResult> (
                [FromQuery] string? tableName,
                [FromQuery] DateTime? fromTime,
                [FromQuery] DateTime? toTime,
                [FromQuery] int? operation,
                [FromQuery] string? userId,
                [FromQuery] string? entityId,
                [FromQuery] string? transactionId,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                [FromServices] IDiffService diffService,
                CancellationToken cancellationToken) =>
            {
                var take = Math.Clamp(limit ?? 50, 1, 500);
                var filter = new DiffFilterRequest(
                    tableName,
                    fromTime,
                    toTime,
                    operation,
                    userId,
                    entityId,
                    transactionId);
                var result = await diffService.GetFilteredAsync(filter, cursor, take, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetFilteredDiffs")
            .WithTags("Diffs");

        return app;
    }
}
