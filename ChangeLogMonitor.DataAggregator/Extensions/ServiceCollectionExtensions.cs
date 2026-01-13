using ChangeLogMonitor.DataAggregator.Configuration;
using ChangeLogMonitor.DataAggregator.Processing;
using ChangeLogMonitor.DataAggregator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLogMonitor.DataAggregator.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataAggregator(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AppSettings>()
            .Bind(configuration.GetSection(AppSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(settings => settings.Kafka.InputCdcTopics?.Length > 0,
                "Kafka:InputCdcTopics must contain at least one topic")
            .Validate(settings => settings.Processing.HardTtlMs > settings.Processing.FlushIntervalMs,
                "Processing:HardTtlMs must be greater than Processing:FlushIntervalMs")
            .ValidateOnStart();

        services.AddSingleton<TxAggregatorMetrics>();
        services.AddSingleton<TxAggregatorTopologyFactory>();
        services.AddSingleton<TxAggregatorHostedService>();
        services.AddSingleton<ITxAggregatorHealth>(sp => sp.GetRequiredService<TxAggregatorHostedService>());
        services.AddHostedService(sp => sp.GetRequiredService<TxAggregatorHostedService>());

        return services;
    }
}
