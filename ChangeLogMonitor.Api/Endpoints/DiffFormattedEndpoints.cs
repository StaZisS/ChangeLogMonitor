using ChangeLogMonitor.Finalization.Localization;
using ChangeLogMonitor.Finalization.Models;
using ChangeLogMonitor.Finalization.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ChangeLogMonitor.Api.Endpoints;

public static class DiffFormattedEndpoints
{
    private const string TimezoneHeader = "X-Timezone";

    public static IEndpointRouteBuilder MapDiffFormattedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/diffs/{tableName}/{entityId}", async Task<IResult> (
                string tableName,
                string entityId,
                [FromQuery] int? limit,
                [FromServices] IDiffService diffService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(entityId))
                    return Results.BadRequest(new { message = "tableName and entityId are required." });

                var take = Math.Clamp(limit ?? 50, 1, 500);
                var timezone = GetTimezone(httpContext);
                var response = await diffService.GetByEntityFormattedAsync(tableName, entityId, take, timezone, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetDiffsByEntity")
            .WithTags("Diffs");

        app.MapGet("/diffs/tx/{transactionId}", async Task<IResult> (
                string transactionId,
                [FromQuery] int? limit,
                [FromServices] IDiffService diffService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(transactionId))
                    return Results.BadRequest(new { message = "transactionId is required." });

                var take = Math.Clamp(limit ?? 50, 1, 500);
                var timezone = GetTimezone(httpContext);
                var response = await diffService.GetByTransactionFormattedAsync(transactionId, take, timezone, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetDiffsByTransaction")
            .WithTags("Diffs");

        app.MapGet("/diffs", async Task<IResult> (
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                [FromServices] IDiffService diffService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var take = Math.Clamp(limit ?? 50, 1, 500);
                var timezone = GetTimezone(httpContext);
                var result = await diffService.GetAllFormattedAsync(cursor, take, timezone, cancellationToken);
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
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var take = Math.Clamp(limit ?? 50, 1, 500);
                var timezone = GetTimezone(httpContext);
                var filter = new DiffFilterRequest(
                    tableName,
                    fromTime,
                    toTime,
                    operation,
                    userId,
                    entityId,
                    transactionId);
                var result = await diffService.GetFilteredFormattedAsync(filter, cursor, take, timezone, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetFilteredDiffs")
            .WithTags("Diffs");

        return app;
    }

    private static string GetTimezone(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(TimezoneHeader, out var timezoneHeader) &&
            !string.IsNullOrWhiteSpace(timezoneHeader))
        {
            return timezoneHeader.ToString();
        }

        return AuditLogMessages.DefaultTimezone;
    }
}
