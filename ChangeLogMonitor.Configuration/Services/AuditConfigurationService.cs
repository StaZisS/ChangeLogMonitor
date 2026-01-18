using ChangeLogMonitor.Configuration.Mappers;
using ChangeLogMonitor.Configuration.Providers;
using ChangeLogMonitor.Configuration.Validators;
using ChangeLogMonitor.Core.Enums;
using ChangeLogMonitor.Core.Models.Policy;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ChangeLogMonitor.Configuration.Services;

/// <summary>
///     Сервис для загрузки, валидации и кеширования конфигурации аудита
/// </summary>
public class AuditConfigurationService : IAuditConfigurationService
{
    private readonly object _lock = new();
    private readonly ILogger<AuditConfigurationService>? _logger;
    private readonly AuditPolicyMapper _mapper;
    private readonly IAuditPolicyProvider _provider;
    private readonly YamlAuditPolicyValidator _validator;

    private AuditPolicy? _cachedPolicy;
    private DateTime? _lastLoadTime;

    public AuditConfigurationService(
        IAuditPolicyProvider provider,
        ILogger<AuditConfigurationService>? logger = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = logger;
        _mapper = new AuditPolicyMapper();
        _validator = new YamlAuditPolicyValidator();
    }

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
                var yamlRoot = _provider.Load();

                var validationResult = _validator.Validate(yamlRoot.AuditPolicy);
                if (!validationResult.IsValid)
                {
                    var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                    throw new ValidationException($"Audit policy validation failed: {errors}");
                }

                _logger?.LogDebug("Audit policy validation succeeded");

                _cachedPolicy = _mapper.MapToDomain(yamlRoot.AuditPolicy);
                _lastLoadTime = DateTime.UtcNow;

                _logger?.LogInformation(
                    "Audit policy loaded successfully. Version: {Version}, Mode: {Mode}, Entities: {EntityCount}",
                    _cachedPolicy.Version,
                    _cachedPolicy.Mode,
                    _cachedPolicy.Entities.Count);

                return _cachedPolicy;
            }
            catch (FileNotFoundException ex)
            {
                _logger?.LogError(ex, "Configuration file not found");
                throw new InvalidOperationException(
                    "Audit configuration file not found. Please create changelog-config.yaml", ex);
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

    public async Task<AuditPolicy> GetPolicyAsync(bool forceReload = false,
        CancellationToken cancellationToken = default)
    {
        if (_cachedPolicy != null && !forceReload)
        {
            _logger?.LogDebug("Returning cached audit policy (loaded at {LoadTime})", _lastLoadTime);
            return _cachedPolicy;
        }

        _logger?.LogInformation("Loading audit policy from configuration file (async)");

        try
        {
            var yamlRoot = await _provider.LoadAsync(cancellationToken);

            var validationResult = await _validator.ValidateAsync(yamlRoot.AuditPolicy, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                throw new ValidationException($"Audit policy validation failed: {errors}");
            }

            _logger?.LogDebug("Audit policy validation succeeded");

            lock (_lock)
            {
                _cachedPolicy = _mapper.MapToDomain(yamlRoot.AuditPolicy);
                _lastLoadTime = DateTime.UtcNow;
            }

            _logger?.LogInformation(
                "Audit policy loaded successfully. Version: {Version}, Mode: {Mode}, Entities: {EntityCount}",
                _cachedPolicy.Version,
                _cachedPolicy.Mode,
                _cachedPolicy.Entities.Count);

            return _cachedPolicy;
        }
        catch (FileNotFoundException ex)
        {
            _logger?.LogError(ex, "Configuration file not found");
            throw new InvalidOperationException(
                "Audit configuration file not found. Please create changelog-config.yaml", ex);
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

    public EntityPolicy? GetEntityPolicy(string entityName, bool forceReload = false)
    {
        var policy = GetPolicy(forceReload);

        if (policy.Entities.TryGetValue(entityName, out var entityPolicy)) return entityPolicy;

        _logger?.LogWarning("Entity policy not found for: {EntityName}", entityName);
        return null;
    }

    public bool IsEntityEnabled(string entityName, bool forceReload = false)
    {
        var policy = GetPolicy(forceReload);

        if (policy.Mode == AuditMode.Whitelist)
            return policy.Entities.ContainsKey(entityName) &&
                   policy.Entities[entityName].Enabled;

        if (policy.Entities.TryGetValue(entityName, out var entityPolicy)) return entityPolicy.Enabled;

        return true;
    }

    public void ReloadConfiguration()
    {
        lock (_lock)
        {
            _logger?.LogInformation("Clearing configuration cache and reloading");
            _cachedPolicy = null;
            _lastLoadTime = null;
        }

        GetPolicy(true);
    }

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

public interface IAuditConfigurationService
{
    AuditPolicy GetPolicy(bool forceReload = false);
    Task<AuditPolicy> GetPolicyAsync(bool forceReload = false, CancellationToken cancellationToken = default);
    EntityPolicy? GetEntityPolicy(string entityName, bool forceReload = false);
    bool IsEntityEnabled(string entityName, bool forceReload = false);
    void ReloadConfiguration();
    ConfigurationInfo GetConfigurationInfo();
}

public class ConfigurationInfo
{
    public bool IsLoaded { get; set; }
    public DateTime? LoadTime { get; set; }
    public string? Version { get; set; }
    public string? Mode { get; set; }
    public int EntityCount { get; set; }
}