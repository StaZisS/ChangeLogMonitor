using ChangeLogMonitor.Configuration.Mappers;
using ChangeLogMonitor.Configuration.Providers;
using ChangeLogMonitor.Configuration.Validators;
using ChangeLogMonitor.Core.Models.Policy;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ChangeLogMonitor.Configuration.Services;

/// <summary>
/// Сервис для загрузки, валидации и кеширования конфигурации аудита
/// </summary>
public class AuditConfigurationService : IAuditConfigurationService
{
    private readonly IAuditPolicyProvider _provider;
    private readonly AuditPolicyMapper _mapper;
    private readonly YamlAuditPolicyValidator _validator;
    private readonly ILogger<AuditConfigurationService>? _logger;

    private AuditPolicy? _cachedPolicy;
    private DateTime? _lastLoadTime;
    private readonly object _lock = new();

    public AuditConfigurationService(
        IAuditPolicyProvider provider,
        ILogger<AuditConfigurationService>? logger = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = logger;
        _mapper = new AuditPolicyMapper();
        _validator = new YamlAuditPolicyValidator();
    }

    /// <summary>
    /// Загружает политику аудита с валидацией и кешированием
    /// </summary>
    public AuditPolicy GetPolicy(bool forceReload = false)
    {
        lock (_lock)
        {
            if (_cachedPolicy != null && !forceReload)
            {
                _logger?.LogDebug("Returning cached audit policy (loaded at {LoadTime})", _lastLoadTime);
                return _cachedPolicy;
            }

            _logger?.LogInformation("Loading audit policy from configuration file");

            try
            {
                // 1. Загружаем YAML
                var yamlRoot = _provider.Load();

                // 2. Валидируем
                var validationResult = _validator.Validate(yamlRoot.AuditPolicy);
                if (!validationResult.IsValid)
                {
                    var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                    throw new ValidationException($"Audit policy validation failed: {errors}");
                }

                _logger?.LogDebug("Audit policy validation succeeded");

                // 3. Маппим в доменную модель
                _cachedPolicy = _mapper.MapToDomain(yamlRoot.AuditPolicy);
                _lastLoadTime = DateTime.UtcNow;

                _logger?.LogInformation("Audit policy loaded successfully. Version: {Version}, Mode: {Mode}, Entities: {EntityCount}",
                    _cachedPolicy.Version,
                    _cachedPolicy.Mode,
                    _cachedPolicy.Entities.Count);

                return _cachedPolicy;
            }
            catch (FileNotFoundException ex)
            {
                _logger?.LogError(ex, "Configuration file not found");
                throw new InvalidOperationException("Audit configuration file not found. Please create changelog-config.yaml", ex);
            }
            catch (ValidationException ex)
            {
                _logger?.LogError(ex, "Validation failed");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load audit policy");
                throw new InvalidOperationException("Failed to load audit policy configuration", ex);
            }
        }
    }

    /// <summary>
    /// Асинхронная загрузка политики
    /// </summary>
    public async Task<AuditPolicy> GetPolicyAsync(bool forceReload = false, CancellationToken cancellationToken = default)
    {
        if (_cachedPolicy != null && !forceReload)
        {
            _logger?.LogDebug("Returning cached audit policy (loaded at {LoadTime})", _lastLoadTime);
            return _cachedPolicy;
        }

        _logger?.LogInformation("Loading audit policy from configuration file (async)");

        try
        {
            // 1. Загружаем YAML
            var yamlRoot = await _provider.LoadAsync(cancellationToken);

            // 2. Валидируем
            var validationResult = await _validator.ValidateAsync(yamlRoot.AuditPolicy, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                throw new ValidationException($"Audit policy validation failed: {errors}");
            }

            _logger?.LogDebug("Audit policy validation succeeded");

            // 3. Маппим в доменную модель
            lock (_lock)
            {
                _cachedPolicy = _mapper.MapToDomain(yamlRoot.AuditPolicy);
                _lastLoadTime = DateTime.UtcNow;
            }

            _logger?.LogInformation("Audit policy loaded successfully. Version: {Version}, Mode: {Mode}, Entities: {EntityCount}",
                _cachedPolicy.Version,
                _cachedPolicy.Mode,
                _cachedPolicy.Entities.Count);

            return _cachedPolicy;
        }
        catch (FileNotFoundException ex)
        {
            _logger?.LogError(ex, "Configuration file not found");
            throw new InvalidOperationException("Audit configuration file not found. Please create changelog-config.yaml", ex);
        }
        catch (ValidationException ex)
        {
            _logger?.LogError(ex, "Validation failed");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load audit policy");
            throw new InvalidOperationException("Failed to load audit policy configuration", ex);
        }
    }

    /// <summary>
    /// Получает политику для конкретной сущности
    /// </summary>
    public EntityPolicy? GetEntityPolicy(string entityName, bool forceReload = false)
    {
        var policy = GetPolicy(forceReload);

        if (policy.Entities.TryGetValue(entityName, out var entityPolicy))
        {
            return entityPolicy;
        }

        _logger?.LogWarning("Entity policy not found for: {EntityName}", entityName);
        return null;
    }

    /// <summary>
    /// Проверяет, включено ли логирование для сущности
    /// </summary>
    public bool IsEntityEnabled(string entityName, bool forceReload = false)
    {
        var policy = GetPolicy(forceReload);

        // В режиме whitelist - проверяем наличие сущности в списке
        if (policy.Mode == Core.Enums.AuditMode.Whitelist)
        {
            return policy.Entities.ContainsKey(entityName) &&
                   (policy.Entities[entityName].Enabled);
        }

        // В режиме blacklist - проверяем что сущность НЕ отключена
        if (policy.Entities.TryGetValue(entityName, out var entityPolicy))
        {
            return entityPolicy.Enabled;
        }

        return true; // В blacklist mode по умолчанию все включено
    }

    /// <summary>
    /// Сбрасывает кеш и перезагружает конфигурацию
    /// </summary>
    public void ReloadConfiguration()
    {
        lock (_lock)
        {
            _logger?.LogInformation("Clearing configuration cache and reloading");
            _cachedPolicy = null;
            _lastLoadTime = null;
        }

        GetPolicy(forceReload: true);
    }

    /// <summary>
    /// Возвращает информацию о текущей загруженной конфигурации
    /// </summary>
    public ConfigurationInfo GetConfigurationInfo()
    {
        lock (_lock)
        {
            return new ConfigurationInfo
            {
                IsLoaded = _cachedPolicy != null,
                LoadTime = _lastLoadTime,
                Version = _cachedPolicy?.Version,
                Mode = _cachedPolicy?.Mode.ToString(),
                EntityCount = _cachedPolicy?.Entities.Count ?? 0
            };
        }
    }
}

/// <summary>
/// Интерфейс сервиса конфигурации аудита
/// </summary>
public interface IAuditConfigurationService
{
    AuditPolicy GetPolicy(bool forceReload = false);
    Task<AuditPolicy> GetPolicyAsync(bool forceReload = false, CancellationToken cancellationToken = default);
    EntityPolicy? GetEntityPolicy(string entityName, bool forceReload = false);
    bool IsEntityEnabled(string entityName, bool forceReload = false);
    void ReloadConfiguration();
    ConfigurationInfo GetConfigurationInfo();
}

/// <summary>
/// Информация о загруженной конфигурации
/// </summary>
public class ConfigurationInfo
{
    public bool IsLoaded { get; set; }
    public DateTime? LoadTime { get; set; }
    public string? Version { get; set; }
    public string? Mode { get; set; }
    public int EntityCount { get; set; }
}
