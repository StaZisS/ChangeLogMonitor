using ChangeLogMonitor.Configuration.Providers;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Core.Interfaces;
using ChangeLogMonitor.Core.Services;
using ChangeLogMonitor.Interceptor.Interceptors;
using ChangeLogMonitor.Interceptor.Models;
using ChangeLogMonitor.Interceptor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLogMonitor.Interceptor.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Добавляет ChangeLog Interceptor со всеми зависимостями
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="auditDbConnectionString">Строка подключения к БД аудита</param>
    /// <param name="configFilePath">Путь к файлу конфигурации (по умолчанию changelog-config.yaml)</param>
    /// <param name="metadataProviderFactory">Фабрика для создания провайдера метаданных (опционально)</param>
    /// <param name="applyMigrations">Автоматически применить EF миграции audit_log при старте</param>
    /// <param name="enumLabelProviderFactory">Фабрика для создания провайдера лейблов enum (опционально)</param>
    /// <returns>Service collection для цепочки вызовов</returns>
    public static IServiceCollection AddChangeLogInterceptor(
        this IServiceCollection services,
        string auditDbConnectionString,
        string? configFilePath = null,
        Func<IServiceProvider, IAuditMetadataProvider>? metadataProviderFactory = null,
        bool applyMigrations = true,
        Func<IServiceProvider, IEnumLabelProvider>? enumLabelProviderFactory = null)
    {
        services.AddDbContext<AuditDbContext>(options =>
        {
            options.UseNpgsql(auditDbConnectionString,
                b => { b.MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName); });
        });
        
        var configPath = ResolveConfigPath(configFilePath);
        ValidateConfigFile(configPath);
        services.AddSingleton<IAuditPolicyProvider>(sp => new YamlAuditPolicyProvider(configPath));
        services.AddSingleton<IAuditConfigurationService, AuditConfigurationService>();
        
        if (metadataProviderFactory != null)
            services.AddScoped(metadataProviderFactory);
        else
            services.AddScoped<IAuditMetadataProvider, DefaultAuditMetadataProvider>();
        
        if (enumLabelProviderFactory != null)
            services.AddSingleton(enumLabelProviderFactory);
        else
            services.AddSingleton<IEnumLabelProvider, AttributeEnumLabelProvider>();
        
        services.AddSingleton<EnumMetadataExtractor>();
        services.AddScoped<ReferenceMetadataExtractor>();
        services.AddScoped<CollectionDeltaExtractor>();
        
        services.AddScoped<AuditMetadataSerializer>();
        
        services.AddScoped<IRawAuditService, RawAuditService>();
        
        services.AddScoped<ChangeLogInterceptor>();

        if (applyMigrations) services.AddHostedService<AuditDbMigrationHostedService>();

        return services;
    }
    
    public static DbContextOptionsBuilder AddChangeLogInterceptor(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider)
    {
        var interceptor = serviceProvider.GetRequiredService<ChangeLogInterceptor>();
        return optionsBuilder.AddInterceptors(interceptor);
    }
    
    private static string ResolveConfigPath(string? configFilePath)
    {
        if (string.IsNullOrWhiteSpace(configFilePath))
        {
            return Path.Combine(AppContext.BaseDirectory, "changelog-config.yaml");
        }
        
        if (Path.IsPathRooted(configFilePath))
        {
            return configFilePath;
        }
        
        return Path.Combine(AppContext.BaseDirectory, configFilePath);
    }
    
    private static void ValidateConfigFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"Файл конфигурации аудита не найден: '{filePath}'. " +
                $"Создайте файл changelog-config.yaml или укажите правильный путь.");
        }
        
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
                $"Убедитесь, что файл не заблокирован другим процессом.", ex);
        }
    }
}