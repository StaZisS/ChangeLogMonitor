using ChangeLogMonitor.Configuration.Providers;
using ChangeLogMonitor.Configuration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLogMonitor.Configuration.Extensions;

/// <summary>
///     Extensions для регистрации сервисов конфигурации аудита в DI контейнере
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Регистрирует сервисы конфигурации аудита
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configFilePath">Путь к файлу changelog-config.yaml (по умолчанию в корне проекта)</param>
    /// <returns>Service collection для цепочки вызовов</returns>
    public static IServiceCollection AddAuditConfiguration(
        this IServiceCollection services,
        string? configFilePath = null)
    {
        var filePath = configFilePath ?? Path.Combine(AppContext.BaseDirectory, "changelog-config.yaml");

        // Регистрируем провайдер
        services.AddSingleton<IAuditPolicyProvider>(sp =>
            new YamlAuditPolicyProvider(filePath));

        // Регистрируем главный сервис конфигурации
        services.AddSingleton<IAuditConfigurationService, AuditConfigurationService>();

        return services;
    }

    /// <summary>
    ///     Регистрирует сервисы конфигурации аудита с указанием пути через конфигурацию
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Действие для настройки опций</param>
    /// <returns>Service collection для цепочки вызовов</returns>
    public static IServiceCollection AddAuditConfiguration(
        this IServiceCollection services,
        Action<AuditConfigurationOptions> configureOptions)
    {
        var options = new AuditConfigurationOptions();
        configureOptions(options);

        return services.AddAuditConfiguration(options.ConfigFilePath);
    }
}

/// <summary>
///     Опции для конфигурации аудита
/// </summary>
public class AuditConfigurationOptions
{
    /// <summary>
    ///     Путь к файлу конфигурации
    /// </summary>
    public string ConfigFilePath { get; set; } = "changelog-config.yaml";
}