using System.Security.Claims;
using System.Text;
using Auditmeta.Raw;
using ChangeLogMonitor.Interceptor.Extensions;
using ChangeLogMonitor.Interceptor.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using TestProject.Auth;
using TestProject.Contracts;
using TestProject.Data;
using TestProject.Domain;
using TestProject.Infrastructure;
using static ChangeLogMonitor.Core.Extensions.EnumLabelExtensions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TestProjectDb") ?? "Data Source=test-harness.db";
var auditConnectionString = builder.Configuration.GetConnectionString("AuditDb") ?? connectionString;
var usePostgres = connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase);

builder.Services.AddHttpContextAccessor();

if (usePostgres)
{
    var configPath = builder.Configuration.GetValue<string>("AuditConfiguration:ConfigFilePath")
                     ?? "changelog-config.yaml";

    builder.Services.AddChangeLogInterceptor(
        auditConnectionString,
        configPath,
        sp => new HttpContextAuditMetadataProvider(sp.GetRequiredService<IHttpContextAccessor>()));

    builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    {
        options.UseNpgsql(connectionString);
        options.AddChangeLogInterceptor(sp);
    });
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
    builder.Services.AddSingleton<IRawAuditService>(sp => null!);
}

var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtOptions>(jwtSection);
var jwtOptions = jwtSection.Get<JwtOptions>() ??
                 throw new InvalidOperationException("JWT configuration section is missing.");
var signingKey = Encoding.UTF8.GetBytes(jwtOptions.SigningKey);
builder.Services.AddSingleton<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ChangeLogMonitor Test Harness",
        Version = "v1",
        Description = "Simple user/order API protected by JWT for integration testing."
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter token as: Bearer {your JWT}"
    };

    options.AddSecurityDefinition("Bearer", jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

await app.Services.InitializeAsync();

if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/auth/token", async Task<IResult> (
        LoginRequest request,
        AppDbContext dbContext,
        TokenService tokenService,
        CancellationToken cancellationToken) =>
    {
        var username = request.Username?.Trim();
        var password = request.Password?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Results.BadRequest(new { message = "Username and password are required." });

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash)) return Results.Unauthorized();

        var token = tokenService.Generate(user);
        var response = new TokenResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            new UserSummary(user.Id, user.Username, user.FullName, user.Email));

        return Results.Ok(response);
    })
    .WithName("GenerateToken")
    .WithTags("Auth");

app.MapPost("/orders", async Task<IResult> (
        CreateOrderRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return Results.BadRequest(new { message = "Description is required." });

        if (request.Amount <= 0) return Results.BadRequest(new { message = "Amount must be greater than zero." });

        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return Results.Unauthorized();

        var userExists = await dbContext.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists) return Results.Unauthorized();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            Description = request.Description.Trim(),
            Amount = request.Amount,
            Status = request.Status,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new OrderResponse(order.Id, order.Description, order.Amount, order.Status,
            order.Status.ToString(), order.CreatedAt, order.UserId);
        return Results.Created($"/orders/{order.Id}", response);
    })
    .WithName("CreateOrder")
    .WithTags("Orders")
    .RequireAuthorization();

app.MapPut("/orders/{orderId:guid}", async Task<IResult> (
        Guid orderId,
        UpdateOrderRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return Results.BadRequest(new { message = "Description is required." });

        if (request.Amount <= 0) return Results.BadRequest(new { message = "Amount must be greater than zero." });

        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return Results.Unauthorized();

        var userExists = await dbContext.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists) return Results.Unauthorized();

        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null) return Results.NotFound();

        if (order.UserId != userId) return Results.Forbid();

        order.Description = request.Description.Trim();
        order.Amount = request.Amount;
        if (request.Status.HasValue)
            order.Status = request.Status.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new OrderResponse(order.Id, order.Description, order.Amount, order.Status,
            order.Status.ToString(), order.CreatedAt, order.UserId);
        return Results.Ok(response);
    })
    .WithName("UpdateOrder")
    .WithTags("Orders")
    .RequireAuthorization();

app.MapPatch("/orders/{orderId:guid}/reassign", async Task<IResult> (
        Guid orderId,
        ReassignOrderRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var currentUserId)) return Results.Unauthorized();

        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null) return Results.NotFound();

        if (order.UserId != currentUserId) return Results.Forbid();

        var newUserExists = await dbContext.Users.AnyAsync(u => u.Id == request.NewUserId, cancellationToken);
        if (!newUserExists) return Results.BadRequest(new { message = "Target user does not exist." });

        order.UserId = request.NewUserId;
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new OrderResponse(order.Id, order.Description, order.Amount, order.Status,
            order.Status.ToString(), order.CreatedAt, order.UserId);
        return Results.Ok(response);
    })
    .WithName("ReassignOrder")
    .WithTags("Orders")
    .RequireAuthorization();

app.MapPatch("/orders/{orderId:guid}/status", async Task<IResult> (
        Guid orderId,
        ChangeOrderStatusRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var currentUserId)) return Results.Unauthorized();

        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null) return Results.NotFound();

        if (order.UserId != currentUserId) return Results.Forbid();

        order.Status = request.Status;
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new OrderResponse(order.Id, order.Description, order.Amount, order.Status,
            order.Status.ToString(), order.CreatedAt, order.UserId);
        return Results.Ok(response);
    })
    .WithName("ChangeOrderStatus")
    .WithTags("Orders")
    .RequireAuthorization();

app.MapGet("/tags", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
    {
        var tags = await dbContext.Tags
            .AsNoTracking()
            .Select(t => new TagResponse(t.Id, t.Name, t.Color))
            .ToListAsync(cancellationToken);
        return Results.Ok(tags);
    })
    .WithName("GetTags")
    .WithTags("Tags");

app.MapPost("/tags", async Task<IResult> (
        CreateTagRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Name is required." });

        var exists = await dbContext.Tags.AnyAsync(t => t.Name == request.Name.Trim(), cancellationToken);
        if (exists) return Results.Conflict(new { message = "Tag with this name already exists." });

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Color = request.Color?.Trim()
        };

        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new TagResponse(tag.Id, tag.Name, tag.Color);
        return Results.Created($"/tags/{tag.Id}", response);
    })
    .WithName("CreateTag")
    .WithTags("Tags")
    .RequireAuthorization();

app.MapPut("/tags/{tagId:guid}", async Task<IResult> (
        Guid tagId,
        UpdateTagRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Name is required." });

        var tag = await dbContext.Tags.FirstOrDefaultAsync(t => t.Id == tagId, cancellationToken);
        if (tag is null) return Results.NotFound();

        var duplicate =
            await dbContext.Tags.AnyAsync(t => t.Name == request.Name.Trim() && t.Id != tagId, cancellationToken);
        if (duplicate) return Results.Conflict(new { message = "Tag with this name already exists." });

        tag.Name = request.Name.Trim();
        tag.Color = request.Color?.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new TagResponse(tag.Id, tag.Name, tag.Color);
        return Results.Ok(response);
    })
    .WithName("UpdateTag")
    .WithTags("Tags")
    .RequireAuthorization();

app.MapDelete("/tags/{tagId:guid}", async Task<IResult> (
        Guid tagId,
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        var tag = await dbContext.Tags.FirstOrDefaultAsync(t => t.Id == tagId, cancellationToken);
        if (tag is null) return Results.NotFound();

        dbContext.Tags.Remove(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    })
    .WithName("DeleteTag")
    .WithTags("Tags")
    .RequireAuthorization();

app.MapGet("/orders/{orderId:guid}/tags", async Task<IResult> (
        Guid orderId,
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(o => o.OrderTags)
            .ThenInclude(ot => ot.Tag)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null) return Results.NotFound();

        var tags = order.OrderTags
            .Where(ot => ot.Tag is not null)
            .Select(ot => new TagResponse(ot.Tag!.Id, ot.Tag.Name, ot.Tag.Color))
            .ToList();

        return Results.Ok(tags);
    })
    .WithName("GetOrderTags")
    .WithTags("OrderTags");

app.MapPost("/orders/{orderId:guid}/tags", async Task<IResult> (
        Guid orderId,
        AddTagToOrderRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return Results.Unauthorized();

        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null) return Results.NotFound();

        if (order.UserId != userId) return Results.Forbid();

        var tagExists = await dbContext.Tags.AnyAsync(t => t.Id == request.TagId, cancellationToken);
        if (!tagExists) return Results.BadRequest(new { message = "Tag does not exist." });

        var alreadyAssigned = await dbContext.OrderTags
            .AnyAsync(ot => ot.OrderId == orderId && ot.TagId == request.TagId, cancellationToken);
        if (alreadyAssigned) return Results.Conflict(new { message = "Tag already assigned to this order." });

        var orderTag = new OrderTag
        {
            OrderId = orderId,
            TagId = request.TagId,
            AssignedAt = DateTimeOffset.UtcNow
        };

        dbContext.OrderTags.Add(orderTag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/orders/{orderId}/tags/{request.TagId}", null);
    })
    .WithName("AddTagToOrder")
    .WithTags("OrderTags")
    .RequireAuthorization();

app.MapDelete("/orders/{orderId:guid}/tags/{tagId:guid}", async Task<IResult> (
        Guid orderId,
        Guid tagId,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return Results.Unauthorized();

        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null) return Results.NotFound();

        if (order.UserId != userId) return Results.Forbid();

        var orderTag = await dbContext.OrderTags
            .FirstOrDefaultAsync(ot => ot.OrderId == orderId && ot.TagId == tagId, cancellationToken);
        if (orderTag is null) return Results.NotFound();

        dbContext.OrderTags.Remove(orderTag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    })
    .WithName("RemoveTagFromOrder")
    .WithTags("OrderTags")
    .RequireAuthorization();

app.MapPost("/orders/batch/update-status", async Task<IResult> (
        BatchUpdateStatusRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        IRawAuditService? rawAuditService,
        CancellationToken cancellationToken) =>
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return Results.Unauthorized();

        if (rawAuditService is null)
            return Results.BadRequest(new { message = "Raw audit service not available. Use PostgreSQL." });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var affected = await dbContext.Orders
                .Where(o => o.UserId == userId && o.Status == request.FromStatus)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(o => o.Status, request.ToStatus),
                    cancellationToken);

            await rawAuditService.RecordRawOperationAsync(
                dbContext,
                "Orders",
                affected,
                $"Batch status update: {request.FromStatus} -> {request.ToStatus}",
                new Dictionary<string, string>
                {
                    ["operation"] = "batch_update_status",
                    ["from_status"] = request.FromStatus.ToString(),
                    ["to_status"] = request.ToStatus.ToString()
                },
                CreateEnumSnapshot(request.FromStatus, request.ToStatus),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Results.Ok(new BulkOperationResult(
                affected,
                "BatchUpdateStatus",
                $"Updated {affected} orders from {request.FromStatus} to {request.ToStatus}"));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    })
    .WithName("BatchUpdateOrderStatus")
    .WithTags("BulkOperations")
    .RequireAuthorization();

app.MapPost("/orders/batch/delete-by-status", async Task<IResult> (
        BatchDeleteByStatusRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        IRawAuditService? rawAuditService,
        CancellationToken cancellationToken) =>
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return Results.Unauthorized();

        if (rawAuditService is null)
            return Results.BadRequest(new { message = "Raw audit service not available. Use PostgreSQL." });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var deletedTags = await dbContext.OrderTags
                .Where(ot => dbContext.Orders
                    .Where(o => o.UserId == userId && o.Status == request.Status)
                    .Select(o => o.Id)
                    .Contains(ot.OrderId))
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedTags > 0)
                await rawAuditService.RecordRawOperationAsync(
                    dbContext,
                    "OrderTags",
                    deletedTags,
                    $"Cascade delete order tags for status {request.Status}",
                    cancellationToken: cancellationToken);

            var affected = await dbContext.Orders
                .Where(o => o.UserId == userId && o.Status == request.Status)
                .ExecuteDeleteAsync(cancellationToken);

            await rawAuditService.RecordRawOperationAsync(
                dbContext,
                "Orders",
                affected,
                $"Batch delete orders with status {request.Status}",
                new Dictionary<string, string>
                {
                    ["operation"] = "batch_delete",
                    ["status"] = request.Status.ToString()
                },
                CreateEnumSnapshot(request.Status),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Results.Ok(new BulkOperationResult(
                affected,
                "BatchDeleteByStatus",
                $"Deleted {affected} orders with status {request.Status}"));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    })
    .WithName("BatchDeleteOrdersByStatus")
    .WithTags("BulkOperations")
    .RequireAuthorization();

app.MapPost("/orders/raw/update-by-amount", async Task<IResult> (
        RawUpdateRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        IRawAuditService? rawAuditService,
        CancellationToken cancellationToken) =>
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return Results.Unauthorized();

        if (rawAuditService is null)
            return Results.BadRequest(new { message = "Raw audit service not available. Use PostgreSQL." });

        if (request.MinAmount >= request.MaxAmount)
            return Results.BadRequest(new { message = "MinAmount must be less than MaxAmount." });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var affected = await dbContext.ExecuteSqlRawWithAuditAsync(
                rawAuditService,
                "Orders",
                @"UPDATE ""Orders""
                       SET ""Status"" = {0}
                       WHERE ""UserId"" = {1}
                         AND ""Amount"" >= {2}
                         AND ""Amount"" <= {3}",
                new object[] { (int)request.NewStatus, userId, request.MinAmount, request.MaxAmount },
                request.Reason ?? $"Raw update: set status to {request.NewStatus} for amount range",
                new Dictionary<string, string>
                {
                    ["operation"] = "raw_update",
                    ["min_amount"] = request.MinAmount.ToString("F2"),
                    ["max_amount"] = request.MaxAmount.ToString("F2"),
                    ["new_status"] = request.NewStatus.ToString()
                },
                CreateFullEnumSnapshot<OrderStatus>(),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Results.Ok(new BulkOperationResult(
                affected,
                "RawUpdateByAmount",
                $"Updated {affected} orders with amount between {request.MinAmount:F2} and {request.MaxAmount:F2}"));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    })
    .WithName("RawUpdateOrdersByAmount")
    .WithTags("BulkOperations")
    .RequireAuthorization();

app.MapPost("/orders/raw/cancel-old", async Task<IResult> (
        int olderThanDays,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        IRawAuditService? rawAuditService,
        CancellationToken cancellationToken) =>
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return Results.Unauthorized();

        if (rawAuditService is null)
            return Results.BadRequest(new { message = "Raw audit service not available. Use PostgreSQL." });

        if (olderThanDays < 1)
            return Results.BadRequest(new { message = "olderThanDays must be at least 1." });

        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-olderThanDays);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            using (AuditScope.Begin()
                       .AddHint("operation", "cancel_old_orders")
                       .AddHint("cutoff_date", cutoffDate.ToString("O"))
                       .AddHint("older_than_days", olderThanDays.ToString()))
            {
                var affected = await dbContext.Database.ExecuteSqlRawAsync(
                    @"UPDATE ""Orders""
                      SET ""Status"" = {0}
                      WHERE ""UserId"" = {1}
                        AND ""CreatedAt"" < {2}
                        AND ""Status"" NOT IN ({3}, {4})",
                    new object[]
                    {
                        (int)OrderStatus.Cancelled,
                        userId,
                        cutoffDate,
                        (int)OrderStatus.Delivered,
                        (int)OrderStatus.Cancelled
                    },
                    cancellationToken);

                await rawAuditService.RecordRawOperationAsync(
                    dbContext,
                    "Orders",
                    affected,
                    $"Cancel orders older than {olderThanDays} days",
                    enumSnapshots: CreateFullEnumSnapshot<OrderStatus>(),
                    cancellationToken: cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return Results.Ok(new BulkOperationResult(
                    affected,
                    "CancelOldOrders",
                    $"Cancelled {affected} orders older than {olderThanDays} days"));
            }
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    })
    .WithName("CancelOldOrders")
    .WithTags("BulkOperations")
    .RequireAuthorization();

app.MapGet("/debug/audit-logs", async Task<IResult> (
        int? limit,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken) =>
    {
        var connString = configuration.GetConnectionString("AuditDb")
                         ?? configuration.GetConnectionString("TestProjectDb");

        if (string.IsNullOrWhiteSpace(connString) || !connString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { message = "PostgreSQL connection string not configured." });

        var take = Math.Clamp(limit ?? 10, 1, 100);
        var results = new List<object>();

        await using var connection = new NpgsqlConnection(connString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT id, created_at, transaction_id, payload FROM audit_log ORDER BY id DESC LIMIT {take}";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt64(0);
            var createdAt = reader.GetDateTime(1);
            var transactionId = reader.GetString(2);
            var payloadBytes = reader.GetFieldValue<byte[]>(3);

            object? payloadJson = null;
            string? error = null;

            try
            {
                var envelope = AuditMetaEnvelope.Parser.ParseFrom(payloadBytes);
                payloadJson = new
                {
                    transactionId = envelope.TransactionId,
                    createdAtUtcMs = envelope.CreatedAtUtcMs,
                    actor = envelope.Actor != null
                        ? new
                        {
                            userId = envelope.Actor.UserId,
                            userName = envelope.Actor.UserName
                        }
                        : null,
                    request = envelope.Request != null
                        ? new
                        {
                            requestId = envelope.Request.RequestId,
                            serviceName = envelope.Request.HasServiceName ? envelope.Request.ServiceName : null,
                            clientIp = envelope.Request.HasClientIp ? envelope.Request.ClientIp : null,
                            userAgent = envelope.Request.HasUserAgent ? envelope.Request.UserAgent : null
                        }
                        : null,
                    bulk = envelope.Bulk != null
                        ? new
                        {
                            isBulk = envelope.Bulk.IsBulk,
                            affectedCount = envelope.Bulk.AffectedCount,
                            target = envelope.Bulk.Target
                        }
                        : null,
                    hints = envelope.Hints.Select(h => new { key = h.Key, value = h.Value }).ToList(),
                    enumSnapshots = envelope.EnumSnapshots.Select(es => new
                    {
                        enumType = es.EnumType,
                        pairs = es.Pairs.Select(p => new { code = p.Code, label = p.Label }).ToList()
                    }).ToList(),
                    referenceSnapshots = envelope.ReferenceSnapshots.Select(rs => new
                    {
                        entityType = rs.EntityType,
                        fieldName = rs.FieldName,
                        relatedEntityType = rs.RelatedEntityType,
                        key = rs.Key,
                        title = rs.Title
                    }).ToList(),
                    collectionDeltas = envelope.CollectionDeltas.Select(cd => new
                    {
                        entityType = cd.EntityType,
                        entityId = cd.EntityId,
                        fieldName = cd.FieldName,
                        relatedEntityType = cd.RelatedEntityType,
                        added = cd.Added.Select(a => new { key = a.Key, title = a.Title }).ToList(),
                        removed = cd.Removed.Select(r => new { key = r.Key, title = r.Title }).ToList()
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            results.Add(new
            {
                id,
                createdAt,
                transactionId,
                payloadBase64 = Convert.ToBase64String(payloadBytes),
                payloadJson,
                parseError = error
            });
        }

        return Results.Ok(results);
    })
    .WithName("GetAuditLogsDebug")
    .WithTags("Debug");

app.Run();