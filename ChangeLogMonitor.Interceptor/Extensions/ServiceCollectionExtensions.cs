using ChangeLogMonitor.Configuration.Providers;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Interceptor.Interceptors;
using ChangeLogMonitor.Interceptor.Models;
using ChangeLogMonitor.Interceptor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLogMonitor.Interceptor.Extensions;

/// <summary>
///     Extension методы для регистрации ChangeLog Interceptor в DI контейнере
/// </summary>
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
    /// <returns>Service collection для цепочки вызовов</returns>
    public static IServiceCollection AddChangeLogInterceptor(
        this IServiceCollection services,
        string auditDbConnectionString,
        string? configFilePath = null,
        Func<IServiceProvider, IAuditMetadataProvider>? metadataProviderFactory = null,
        bool applyMigrations = true)
    {
        // Регистрируем AuditDbContext
        services.AddDbContext<AuditDbContext>(options =>
        {
            options.UseNpgsql(auditDbConnectionString,
                b => { b.MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName); });
        });

        // Регистрируем конфигурацию
        var configPath = configFilePath ?? "changelog-config.yaml";
        services.AddSingleton<IAuditPolicyProvider>(sp => new YamlAuditPolicyProvider(configPath));
        services.AddSingleton<IAuditConfigurationService, AuditConfigurationService>();

        // Регистрируем провайдер метаданных
        if (metadataProviderFactory != null)
            services.AddScoped(metadataProviderFactory);
        else
            services.AddScoped<IAuditMetadataProvider, DefaultAuditMetadataProvider>();

        // Регистрируем сериализатор
        services.AddScoped<AuditMetadataSerializer>();

        // Регистрируем интерцептор
        services.AddScoped<ChangeLogInterceptor>();

        if (applyMigrations) services.AddHostedService<AuditDbMigrationHostedService>();

        return services;
    }

    /// <summary>
    ///     Добавляет ChangeLog Interceptor к DbContextOptionsBuilder
    /// </summary>
    public static DbContextOptionsBuilder AddChangeLogInterceptor(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider)
    {
        var interceptor = serviceProvider.GetRequiredService<ChangeLogInterceptor>();
        return optionsBuilder.AddInterceptors(interceptor);
    }
}