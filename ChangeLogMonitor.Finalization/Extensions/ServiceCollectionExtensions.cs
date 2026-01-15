using ChangeLogMonitor.Configuration.Providers;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Core.Interfaces;
using ChangeLogMonitor.Finalization.Options;
using ChangeLogMonitor.Finalization.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ChangeLogMonitor.Finalization.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFinalization(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        services
            .AddOptions<AppSettings>()
            .Bind(configuration.GetSection(AppSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Kafka.InputTopic),
                "Finalizer:Kafka:InputTopic is required")
            .ValidateOnStart();

        services.AddSingleton<IAuditConfigurationService>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AppSettings>>().Value;
            var path = settings.Policy?.ConfigPath ?? "changelog-config.yaml";
            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

            // Validate config file exists
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    $"Файл конфигурации аудита не найден: '{fullPath}'. " +
                    $"Убедитесь что файл changelog-config.yaml существует.");
            }

            return new AuditConfigurationService(
                new YamlAuditPolicyProvider(fullPath),
                sp.GetService<ILogger<AuditConfigurationService>>());
        });

        services.AddSingleton<IAggregateFlattener, AggregateFlattener>();
        services.AddSingleton<IAuditLogRepository, ClickHouseAuditLogRepository>();
        services.AddSingleton<IAuditLogFormatter, AuditLogFormatter>();

        // Access Control Services
        services.AddSingleton<IAccessControlService, AccessControlService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();

        services.AddScoped<IDiffService, DiffService>();
        services.AddHostedService<AggregateIngestService>();

        return services;
    }

    public static async Task EnsureFinalizationSchemaAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Finalization");
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<AppSettings>>().Value;

        if (!settings.ClickHouse.EnsureSchema)
        {
            logger?.LogInformation("ClickHouse schema initialization skipped (EnsureSchema=false)");
            return;
        }

        try
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
            await repository.EnsureSchemaAsync(cancellationToken);
            logger?.LogInformation("ClickHouse schema initialized successfully");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to initialize ClickHouse schema. /diffs endpoints will not work until ClickHouse is available");
        }
    }
}
