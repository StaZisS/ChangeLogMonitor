using ChangeLogMonitor.DataAggregator.Configuration;
using ChangeLogMonitor.DataAggregator.Processing;
using ChangeLogMonitor.DataAggregator.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services
    .AddOptions<AppSettings>()
    .Bind(builder.Configuration.GetSection(AppSettings.SectionName))
    .ValidateDataAnnotations()
    .Validate(settings => settings.Kafka.InputCdcTopics?.Length > 0,
        "Kafka:InputCdcTopics must contain at least one topic")
    .Validate(settings => settings.Processing.HardTtlMs > settings.Processing.FlushIntervalMs,
        "Processing:HardTtlMs must be greater than Processing:FlushIntervalMs")
    .ValidateOnStart();

builder.Services.AddSingleton<TxAggregatorMetrics>();
builder.Services.AddSingleton<TxAggregatorTopologyFactory>();
builder.Services.AddSingleton<TxAggregatorHostedService>();
builder.Services.AddSingleton<ITxAggregatorHealth>(sp => sp.GetRequiredService<TxAggregatorHostedService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TxAggregatorHostedService>());

var httpPort = builder.Configuration.GetValue<int?>($"{AppSettings.SectionName}:{nameof(AppSettings.Http)}:{nameof(HttpSettings.Port)}") ?? 8080;
builder.WebHost.UseUrls($"http://0.0.0.0:{httpPort}");

var app = builder.Build();

app.MapGet("/healthz", (ITxAggregatorHealth health) =>
{
    var snapshot = health.GetSnapshot();
    return Results.Json(new
    {
        status = health.State,
        running = health.IsRunning,
        ready = health.IsReady,
        metrics = snapshot
    });
});

app.Logger.LogInformation("ChangeLogMonitor.DataAggregator listening on http://0.0.0.0:{Port}", httpPort);

await app.RunAsync();
