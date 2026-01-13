using ChangeLogMonitor.Finalization.Models;
using ChangeLogMonitor.Finalization.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
                int? limit,
                IAuditLogRepository repository,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(entityId))
                    return Results.BadRequest(new { message = "tableName and entityId are required." });

                var take = Math.Clamp(limit ?? 50, 1, 500);
                var records = await repository.GetByEntityAsync(tableName, entityId, take, cancellationToken);
                var response = records.Select(DiffResponse.FromRecord);
                return Results.Ok(response);
            })
            .WithName("GetDiffsByEntity")
            .WithTags("Diffs");

        app.MapGet("/diffs/tx/{transactionId}", async Task<IResult> (
                string transactionId,
                int? limit,
                IAuditLogRepository repository,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(transactionId))
                    return Results.BadRequest(new { message = "transactionId is required." });

                var take = Math.Clamp(limit ?? 50, 1, 500);
                var records = await repository.GetByTransactionAsync(transactionId, take, cancellationToken);
                var response = records.Select(DiffResponse.FromRecord);
                return Results.Ok(response);
            })
            .WithName("GetDiffsByTransaction")
            .WithTags("Diffs");

        app.MapGet("/diffs", async Task<IResult> (
                int? page,
                int? pageSize,
                IAuditLogRepository repository,
                CancellationToken cancellationToken) =>
            {
                var currentPage = Math.Max(page ?? 1, 1);
                var size = Math.Clamp(pageSize ?? 50, 1, 500);
                var offset = (currentPage - 1) * size;

                var (records, totalCount) = await repository.GetAllAsync(offset, size, cancellationToken);
                var totalPages = (int)Math.Ceiling((double)totalCount / size);

                return Results.Ok(new
                {
                    data = records.Select(DiffResponse.FromRecord),
                    pagination = new
                    {
                        page = currentPage,
                        pageSize = size,
                        totalCount,
                        totalPages
                    }
                });
            })
            .WithName("GetAllDiffs")
            .WithTags("Diffs");

        return app;
    }
}
