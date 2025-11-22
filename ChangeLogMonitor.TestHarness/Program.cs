using System.Security.Claims;
using System.Text;
using ChangeLogMonitor.Interceptor.Extensions;
using ChangeLogMonitor.TestHarness.Auth;
using ChangeLogMonitor.TestHarness.Contracts;
using ChangeLogMonitor.TestHarness.Data;
using ChangeLogMonitor.TestHarness.Domain;
using ChangeLogMonitor.TestHarness.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TestHarnessDb") ?? "Data Source=test-harness.db";
var auditConnectionString = builder.Configuration.GetConnectionString("AuditDb") ?? connectionString;
var usePostgres = connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase);

if (usePostgres)
{
    var configPath = Path.Combine(AppContext.BaseDirectory, "changelog-config.yaml");

    builder.Services.AddChangeLogInterceptor(auditConnectionString, configPath);

    builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    {
        options.UseNpgsql(connectionString);
        options.AddChangeLogInterceptor(sp);
    });
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
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
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new OrderResponse(order.Id, order.Description, order.Amount, order.CreatedAt, order.UserId);
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

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new OrderResponse(order.Id, order.Description, order.Amount, order.CreatedAt, order.UserId);
        return Results.Ok(response);
    })
    .WithName("UpdateOrder")
    .WithTags("Orders")
    .RequireAuthorization();

app.Run();