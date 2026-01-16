using ChangeLogMonitor.Finalization.Models;
using ChangeLogMonitor.Finalization.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ChangeLogMonitor.Api.Endpoints;

public static class DiffRawEndpoints
{
    public static IEndpointRouteBuilder MapDiffRawEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/diffs/raw/{tableName}/{entityId}", async Task<IResult> (
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
            .WithName("GetRawDiffsByEntity")
            .WithTags("Diffs Raw");

        app.MapGet("/diffs/raw/tx/{transactionId}", async Task<IResult> (
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
            .WithName("GetRawDiffsByTransaction")
            .WithTags("Diffs Raw");

        app.MapGet("/diffs/raw", async Task<IResult> (
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                [FromServices] IDiffService diffService,
                CancellationToken cancellationToken) =>
            {
                var take = Math.Clamp(limit ?? 50, 1, 500);
                var result = await diffService.GetAllAsync(cursor, take, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetAllRawDiffs")
            .WithTags("Diffs Raw");

        app.MapGet("/diffs/raw/filter", async Task<IResult> (
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
            .WithName("GetFilteredRawDiffs")
            .WithTags("Diffs Raw");

        return app;
    }
}
