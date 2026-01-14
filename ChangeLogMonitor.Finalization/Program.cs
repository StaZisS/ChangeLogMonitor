using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLogMonitor.Configuration.Providers;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Finalization.Extensions;
using ChangeLogMonitor.Finalization.Models;
using ChangeLogMonitor.Finalization.Options;
using ChangeLogMonitor.Finalization.Services;
using Microsoft.Extensions.Options;

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

builder.Services.AddSingleton<IAuditConfigurationService>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<AppSettings>>().Value;
    var path = settings.Policy?.ConfigPath ?? "changelog-config.yaml";
    var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(builder.Environment.ContentRootPath, path);
    return new AuditConfigurationService(new YamlAuditPolicyProvider(fullPath), sp.GetService<ILogger<AuditConfigurationService>>());
});

builder.Services.AddSingleton<IAggregateFlattener, AggregateFlattener>();
builder.Services.AddSingleton<IAuditLogRepository, ClickHouseAuditLogRepository>();
builder.Services.AddSingleton<IAuditLogFormatter, AuditLogFormatter>();
builder.Services.AddSingleton<IDiffService, DiffService>();
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

app.MapFinalizationEndpoints();

app.Logger.LogInformation("ChangeLogMonitor.Finalization is running on http://0.0.0.0:{Port}", httpPort);

await app.RunAsync();
