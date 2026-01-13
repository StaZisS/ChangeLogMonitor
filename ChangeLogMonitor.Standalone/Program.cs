using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLogMonitor.DataAggregator.Extensions;
using ChangeLogMonitor.DataAggregator.Services;
using ChangeLogMonitor.Finalization.Extensions;

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

// Register DataAggregator services (Kafka Streams for CDC aggregation)
builder.Services.AddDataAggregator(builder.Configuration);

// Register Finalization services (Kafka consumer -> ClickHouse storage)
builder.Services.AddFinalization(builder.Configuration, builder.Environment.ContentRootPath);

// ASP.NET Core services
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Ensure ClickHouse schema exists
await app.Services.EnsureFinalizationSchemaAsync(app.Lifetime.ApplicationStopping);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection(); // Disabled - using HTTP only
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Combined health endpoint
app.MapGet("/healthz", (ITxAggregatorHealth aggregatorHealth) =>
{
    var aggregatorSnapshot = aggregatorHealth.GetSnapshot();
    return Results.Json(new
    {
        status = "ok",
        service = "standalone",
        aggregator = new
        {
            status = aggregatorHealth.State,
            running = aggregatorHealth.IsRunning,
            ready = aggregatorHealth.IsReady,
            metrics = aggregatorSnapshot
        },
        finalizer = new
        {
            status = "ok"
        }
    });
}).WithName("CombinedHealth").WithTags("Health");

// Map module endpoints
app.MapDataAggregatorEndpoints();
app.MapFinalizationEndpoints();

// Map Razor Pages and Controllers
app.MapRazorPages();
app.MapControllers();

app.Logger.LogInformation("ChangeLogMonitor.Standalone starting...");

app.Run("http://0.0.0.0:5000");
