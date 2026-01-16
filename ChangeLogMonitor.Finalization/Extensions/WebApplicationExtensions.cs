using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Core.Interfaces;
using ChangeLogMonitor.Finalization.Localization;
using ChangeLogMonitor.Finalization.Models;
using ChangeLogMonitor.Finalization.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ChangeLogMonitor.Finalization.Extensions;

public static class WebApplicationExtensions
{
    private const string TimezoneHeader = "X-Timezone";

    public static IEndpointRouteBuilder MapFinalizationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/finalizer/healthz", () => Results.Json(new
            {
                status = "ok",
                service = "finalizer"
            }))
            .WithName("FinalizerHealth")
            .WithTags("Health");

        // Raw endpoints (original format)
        MapRawEndpoints(app);

        // Formatted endpoints (human-readable)
        MapFormattedEndpoints(app);

        // Debug endpoints
        MapDebugEndpoints(app);

        return app;
    }

    private static void MapRawEndpoints(IEndpointRouteBuilder app)
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
    }

    private static void MapFormattedEndpoints(IEndpointRouteBuilder app)
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

    private static void MapDebugEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/debug/config", (
                [FromServices] IAuditConfigurationService configService) =>
            {
                try
                {
                    var policy = configService.GetPolicy();
                    var entities = policy.Entities.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            enabled = kvp.Value.Enabled,
                            displayName = kvp.Value.DisplayName,
                            onCreate = kvp.Value.OnCreate?.ToString(),
                            onUpdate = kvp.Value.OnUpdate?.ToString(),
                            onDelete = kvp.Value.OnDelete?.ToString(),
                            fieldsCount = kvp.Value.Fields.Count,
                            fields = kvp.Value.Fields.ToDictionary(
                                f => f.Key,
                                f => new { action = f.Value.Action.ToString() })
                        });

                    return Results.Ok(new
                    {
                        version = policy.Version,
                        mode = policy.Mode.ToString(),
                        defaultCulture = policy.DefaultCulture,
                        defaultTimeZone = policy.DefaultTimeZone,
                        entitiesCount = policy.Entities.Count,
                        entities
                    });
                }
                catch (Exception ex)
                {
                    return Results.Ok(new
                    {
                        error = ex.Message,
                        stackTrace = ex.StackTrace
                    });
                }
            })
            .WithName("GetConfigDebug")
            .WithTags("Debug");

        app.MapGet("/debug/audit-logs", async Task<IResult> (
                [FromQuery] int? limit,
                [FromServices] IAuditLogRepository repository,
                CancellationToken cancellationToken) =>
            {
                var take = Math.Clamp(limit ?? 10, 1, 100);
                var records = await repository.GetAllWithCursorAsync(null, take, cancellationToken);

                var results = new List<object>();

                foreach (var record in records)
                {
                    object? payloadJson = null;
                    string? error = null;

                    try
                    {
                        var payloadBytes = Convert.FromBase64String(record.Payload);
                        var auditRecord = Audit.V1.AuditRecord.Parser.ParseFrom(payloadBytes);

                        payloadJson = new
                        {
                            id = auditRecord.Id,
                            entityType = auditRecord.EntityType,
                            entityId = auditRecord.EntityId,
                            entityTitle = auditRecord.EntityTitle,
                            operation = auditRecord.Operation.ToString(),
                            timestampUtc = auditRecord.TimestampUtc?.ToDateTime(),
                            userId = auditRecord.UserId,
                            userTitle = auditRecord.UserTitle,
                            userType = auditRecord.UserType,
                            fieldChanges = auditRecord.FieldChanges.Select(fc => new
                            {
                                fieldName = fc.FieldName,
                                fieldTitle = fc.FieldTitle,
                                valueKind = fc.ValueKind.ToString(),
                                sensitiveMode = fc.SensitiveMode.ToString(),
                                oldValue = fc.OldValue != null ? new
                                {
                                    normalized = fc.OldValue.Normalized,
                                    enumCode = fc.OldValue.EnumCode,
                                    enumTitle = fc.OldValue.EnumTitle,
                                    referenceKey = fc.OldValue.ReferenceKey,
                                    referenceTitle = fc.OldValue.ReferenceTitle
                                } : null,
                                newValue = fc.NewValue != null ? new
                                {
                                    normalized = fc.NewValue.Normalized,
                                    enumCode = fc.NewValue.EnumCode,
                                    enumTitle = fc.NewValue.EnumTitle,
                                    referenceKey = fc.NewValue.ReferenceKey,
                                    referenceTitle = fc.NewValue.ReferenceTitle
                                } : null
                            }).ToList(),
                            collectionChanges = auditRecord.CollectionChanges.Select(cc => new
                            {
                                fieldName = cc.FieldName,
                                fieldTitle = cc.FieldTitle,
                                items = cc.Items.Select(item => new
                                {
                                    kind = item.Kind.ToString(),
                                    itemKey = item.ItemKey,
                                    itemTitle = item.ItemTitle,
                                    rawNormalized = item.RawNormalized
                                }).ToList()
                            }).ToList(),
                            rawPayloadJson = auditRecord.RawPayloadJson,
                            normalizationVersion = auditRecord.NormalizationVersion
                        };
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }

                    results.Add(new
                    {
                        logId = record.LogId,
                        changeTimeUtc = record.ChangeTimeUtc,
                        userId = record.UserId,
                        userName = record.UserName,
                        tableName = record.TableName,
                        operationCode = record.OperationCode,
                        entityId = record.EntityId,
                        txId = record.TxId,
                        payloadBase64 = record.Payload,
                        payloadJson,
                        parseError = error
                    });
                }

                return Results.Ok(results);
            })
            .WithName("GetAuditLogsDebug")
            .WithTags("Debug");

        app.MapGet("/debug/access-control", (
                [FromServices] IAccessControlService accessControl,
                [FromServices] ICurrentUserService currentUser,
                [FromServices] IAuditConfigurationService configService) =>
            {
                try
                {
                    var userId = currentUser.GetUserId();
                    var isAuthenticated = currentUser.IsAuthenticated;
                    var isEnabled = accessControl.IsEnabled;
                    var roles = accessControl.GetUserRoles(userId);
                    var allowedEntities = accessControl.GetAllowedEntities(roles);
                    var hasFullAccess = accessControl.HasFullAccess(userId);
                    var unauthorizedBehavior = accessControl.GetUnauthorizedBehavior();

                    var policy = configService.GetPolicy();
                    var accessControlConfig = policy.AccessControl;

                    // Симуляция того что делает DiffService.ApplyAccessControl
                    DiffFilterRequest? simulatedFilter = null;
                    if (isEnabled)
                    {
                        simulatedFilter = new DiffFilterRequest(null, null, null, null, null, null, null)
                        {
                            AllowedTableNames = allowedEntities.ToList()
                        };
                    }

                    return Results.Ok(new
                    {
                        currentUser = new
                        {
                            userId,
                            isAuthenticated
                        },
                        accessControl = new
                        {
                            isEnabled,
                            unauthorizedBehavior = unauthorizedBehavior.ToString(),
                            hasFullAccess,
                            userRoles = roles,
                            allowedEntities,
                            simulatedFilterAllowedTableNames = simulatedFilter?.AllowedTableNames
                        },
                        config = new
                        {
                            enabled = accessControlConfig.Enabled,
                            allowAnonymous = accessControlConfig.AllowAnonymous,
                            anonymousRoles = accessControlConfig.AnonymousRoles,
                            defaultRoles = accessControlConfig.DefaultRoles,
                            definedRoles = accessControlConfig.Roles.ToDictionary(
                                r => r.Key,
                                r => new { description = r.Value.Description, allowAll = r.Value.AllowAll }),
                            usersMapping = accessControlConfig.Users.ToDictionary(
                                u => u.Key,
                                u => u.Value.Roles)
                        },
                        entitiesAccess = policy.Entities.ToDictionary(
                            e => e.Key,
                            e => new
                            {
                                enabled = e.Value.Enabled,
                                allowedRoles = e.Value.Access.AllowedRoles
                            })
                    });
                }
                catch (Exception ex)
                {
                    return Results.Ok(new
                    {
                        error = ex.Message,
                        stackTrace = ex.StackTrace
                    });
                }
            })
            .WithName("GetAccessControlDebug")
            .WithTags("Debug");
    }
}
