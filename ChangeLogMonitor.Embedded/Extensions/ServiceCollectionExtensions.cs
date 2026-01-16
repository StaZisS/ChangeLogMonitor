using ChangeLogMonitor.Api.Extensions;
using ChangeLogMonitor.Configuration.Providers;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Embedded.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChangeLogMonitor.Embedded.Extensions;

/// <summary>
/// Extension methods for configuring ChangeLogMonitor in embedded mode.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ChangeLogMonitor services for embedded mode.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddChangeLogMonitor(
        this IServiceCollection services,
        Action<EmbeddedChangeLogOptions>? configure = null)
    {
        var options = new EmbeddedChangeLogOptions();
        configure?.Invoke(options);

        // Store options for later use
        services.AddSingleton(options);

        // Register Configuration service
        services.AddSingleton<IAuditConfigurationService>(sp =>
        {
            var opts = sp.GetRequiredService<EmbeddedChangeLogOptions>();
            var fullPath = Path.IsPathRooted(opts.ConfigFilePath)
                ? opts.ConfigFilePath
                : Path.Combine(AppContext.BaseDirectory, opts.ConfigFilePath);

            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    $"Audit configuration file not found: '{fullPath}'. " +
                    $"Ensure changelog-config.yaml exists.");
            }

            return new AuditConfigurationService(
                new YamlAuditPolicyProvider(fullPath),
                sp.GetService<ILogger<AuditConfigurationService>>());
        });

        // Register API services if enabled
        if (options.EnableApi)
        {
            services.AddChangeLogMonitorApi();
        }

        return services;
    }
}
