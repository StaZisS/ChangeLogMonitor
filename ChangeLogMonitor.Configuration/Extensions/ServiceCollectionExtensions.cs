using ChangeLogMonitor.Configuration.Providers;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLogMonitor.Configuration.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Регистрирует сервисы конфигурации аудита
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configFilePath">Путь к файлу changelog-config.yaml (по умолчанию в корне проекта)</param>
    /// <param name="validateOnStartup">Проверять наличие и доступность файла при регистрации (по умолчанию true)</param>
    /// <returns>Service collection для цепочки вызовов</returns>
    public static IServiceCollection AddAuditConfiguration(
        this IServiceCollection services,
        string? configFilePath = null,
        bool validateOnStartup = true)
    {
        var filePath = ResolveConfigPath(configFilePath);
        
        if (validateOnStartup) ValidateConfigFile(filePath);
        
        services.AddSingleton<IAuditPolicyProvider>(sp => new YamlAuditPolicyProvider(filePath));
        services.AddSingleton<IAuditConfigurationService, AuditConfigurationService>();
        services.AddSingleton<IAccessControlService, AccessControlService>();

        return services;
    }

    /// <summary>
    ///     Определяет путь к файлу конфигурации, поддерживая абсолютные и относительные пути
    /// </summary>
    private static string ResolveConfigPath(string? configFilePath)
    {
        if (string.IsNullOrWhiteSpace(configFilePath))
            return Path.Combine(AppContext.BaseDirectory, "changelog-config.yaml");
        
        if (Path.IsPathRooted(configFilePath)) return configFilePath;
        
        return Path.Combine(AppContext.BaseDirectory, configFilePath);
    }

    /// <summary>
    ///     Проверяет существование и доступность файла конфигурации
    /// </summary>
    /// <exception cref="InvalidOperationException">Файл не существует или недоступен</exception>
    private static void ValidateConfigFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new InvalidOperationException(
                $"Файл конфигурации аудита не найден: '{filePath}'. " +
                $"Создайте файл changelog-config.yaml или укажите правильный путь в appsettings.json (AuditConfiguration:ConfigFilePath).");
        
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"Нет доступа к файлу конфигурации аудита: '{filePath}'. " +
                $"Проверьте права доступа к файлу.", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"Не удалось открыть файл конфигурации аудита: '{filePath}'. " +
                $"Убедитесь, что файл не заблокирован другим процессом и не является симлинком без прав доступа.", ex);
        }
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

        return services.AddAuditConfiguration(options.ConfigFilePath, options.ValidateOnStartup);
    }
}

/// <summary>
///     Опции для конфигурации аудита
/// </summary>
public class AuditConfigurationOptions
{
    /// <summary>
    ///     Путь к файлу конфигурации (абсолютный или относительный от BaseDirectory)
    /// </summary>
    public string ConfigFilePath { get; set; } = "changelog-config.yaml";

    /// <summary>
    ///     Проверять наличие и доступность файла при старте приложения
    /// </summary>
    public bool ValidateOnStartup { get; set; } = true;
}