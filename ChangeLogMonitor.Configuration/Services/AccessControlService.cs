using ChangeLogMonitor.Core.Enums;
using ChangeLogMonitor.Core.Interfaces;

namespace ChangeLogMonitor.Configuration.Services;

/// <summary>
///     Реализация сервиса контроля доступа на основе YAML конфигурации
/// </summary>
public class AccessControlService : IAccessControlService
{
    private readonly IAuditConfigurationService _configService;

    public AccessControlService(IAuditConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    /// <inheritdoc />
    public bool IsEnabled => _configService.GetPolicy().AccessControl.Enabled;

    /// <inheritdoc />
    public IReadOnlyList<string> GetUserRoles(string? userId)
    {
        var policy = _configService.GetPolicy();
        var accessControl = policy.AccessControl;

        if (!accessControl.Enabled)
            return Array.Empty<string>();

        // Анонимный пользователь
        if (string.IsNullOrWhiteSpace(userId))
        {
            return accessControl.AllowAnonymous
                ? accessControl.AnonymousRoles.AsReadOnly()
                : Array.Empty<string>();
        }

        // Ищем маппинг для пользователя
        if (accessControl.Users.TryGetValue(userId, out var mapping))
            return mapping.Roles.AsReadOnly();

        // Возвращаем роли по умолчанию
        return accessControl.DefaultRoles.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllowedEntities(IEnumerable<string> roles)
    {
        var policy = _configService.GetPolicy();
        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Если нет ролей - нет доступа ни к чему
        if (roleSet.Count == 0)
            return Array.Empty<string>();

        // Проверяем, есть ли роль с allowAll
        foreach (var role in roleSet)
        {
            if (policy.AccessControl.Roles.TryGetValue(role, out var def) && def.AllowAll)
                return policy.Entities.Keys.ToList().AsReadOnly();
        }

        // Фильтруем сущности по разрешенным ролям
        var allowed = new List<string>();
        foreach (var (entityName, entityPolicy) in policy.Entities)
        {
            if (!entityPolicy.Enabled)
                continue;

            var entityRoles = entityPolicy.Access.AllowedRoles;

            // Если allowedRoles пуст - сущность доступна всем аутентифицированным
            if (entityRoles.Count == 0 || entityRoles.Any(r => roleSet.Contains(r)))
                allowed.Add(entityName);
        }

        return allowed.AsReadOnly();
    }

    /// <inheritdoc />
    public bool CanAccessEntity(string? userId, string entityName)
    {
        var policy = _configService.GetPolicy();

        if (!policy.AccessControl.Enabled)
            return true;

        var roles = GetUserRoles(userId);
        if (roles.Count == 0)
            return false;

        // Проверяем роль с allowAll
        foreach (var role in roles)
        {
            if (policy.AccessControl.Roles.TryGetValue(role, out var def) && def.AllowAll)
                return true;
        }

        // Проверяем доступ к конкретной сущности
        if (!policy.Entities.TryGetValue(entityName, out var entityPolicy))
            return false;

        var allowedRoles = entityPolicy.Access.AllowedRoles;

        // Если allowedRoles пуст - сущность доступна всем аутентифицированным
        if (allowedRoles.Count == 0)
            return true;

        return allowedRoles.Any(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public UnauthorizedBehavior GetUnauthorizedBehavior()
    {
        return _configService.GetPolicy().AccessControl.UnauthorizedBehavior;
    }

    /// <inheritdoc />
    public bool HasFullAccess(string? userId)
    {
        var policy = _configService.GetPolicy();
        var roles = GetUserRoles(userId);

        foreach (var role in roles)
        {
            if (policy.AccessControl.Roles.TryGetValue(role, out var def) && def.AllowAll)
                return true;
        }

        return false;
    }
}
