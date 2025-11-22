using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLogMonitor.Finalization.Models;
using ChangeLogMonitor.Finalization.Options;
using ChangeLogMonitor.Finalization.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true)
    .AddEnvironmentVariables();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services
    .AddOptions<AppSettings>()
    .Bind(builder.Configuration.GetSection(AppSettings.SectionName))
    .ValidateDataAnnotations()
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Kafka.InputTopic), "Kafka:InputTopic is required")
    .ValidateOnStart();

builder.Services.AddSingleton<IAggregateFlattener, AggregateFlattener>();
builder.Services.AddSingleton<IAuditLogRepository, ClickHouseAuditLogRepository>();
builder.Services.AddHostedService<AggregateIngestService>();
builder.Services.AddEndpointsApiExplorer();

var httpPort = builder.Configuration.GetValue<int?>($"{AppSettings.SectionName}:{nameof(HttpSettings.Port)}") ?? 8081;
builder.WebHost.UseUrls($"http://0.0.0.0:{httpPort}");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
    await repository.EnsureSchemaAsync(app.Lifetime.ApplicationStopping);
}

app.MapGet("/healthz", () => Results.Json(new
{
    status = "ok",
    service = "finalizer"
}));

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

app.Logger.LogInformation("ChangeLogMonitor.Finalization is running on http://0.0.0.0:{Port}", httpPort);

await app.RunAsync();