using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLogMonitor.Api.Extensions;
using ChangeLogMonitor.DataAggregator.Extensions;
using ChangeLogMonitor.Finalization.Extensions;
using ChangeLogMonitor.UI.Extensions;

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

// Register API services (HTTP endpoints, CurrentUserService)
builder.Services.AddChangeLogMonitorApi();

// Register UI services
builder.Services.AddAuditLogUI();

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

// Map API endpoints (Health, Diffs, Debug)
app.MapChangeLogMonitorApi();

// Map Razor Pages and Controllers
app.MapRazorPages();
app.MapControllers();

app.Logger.LogInformation("ChangeLogMonitor.Standalone starting...");

app.Run("http://0.0.0.0:5000");