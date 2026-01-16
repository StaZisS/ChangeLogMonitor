using ChangeLogMonitor.Core.Enums;
using ChangeLogMonitor.Core.Interfaces;

namespace ChangeLogMonitor.Configuration.Services;

public class AccessControlService : IAccessControlService
{
    private readonly IAuditConfigurationService _configService;

    public AccessControlService(IAuditConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }
    
    public bool IsEnabled => _configService.GetPolicy().AccessControl.Enabled;
    
    public IReadOnlyList<string> GetUserRoles(string? userId)
    {
        var policy = _configService.GetPolicy();
        var accessControl = policy.AccessControl;

        if (!accessControl.Enabled)
            return Array.Empty<string>();
        
        if (string.IsNullOrWhiteSpace(userId))
        {
            return accessControl.AllowAnonymous
                ? accessControl.AnonymousRoles.AsReadOnly()
                : Array.Empty<string>();
        }
        
        if (accessControl.Users.TryGetValue(userId, out var mapping))
            return mapping.Roles.AsReadOnly();
        
        return accessControl.DefaultRoles.AsReadOnly();
    }
    
    public IReadOnlyList<string> GetAllowedEntities(IEnumerable<string> roles)
    {
        var policy = _configService.GetPolicy();
        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        if (roleSet.Count == 0)
            return Array.Empty<string>();
        
        foreach (var role in roleSet)
        {
            if (policy.AccessControl.Roles.TryGetValue(role, out var def) && def.AllowAll)
                return policy.Entities.Keys.ToList().AsReadOnly();
        }
        
        var allowed = new List<string>();
        foreach (var (entityName, entityPolicy) in policy.Entities)
        {
            if (!entityPolicy.Enabled)
                continue;

            var entityRoles = entityPolicy.Access.AllowedRoles;
            
            if (entityRoles.Count == 0 || entityRoles.Any(r => roleSet.Contains(r)))
                allowed.Add(entityName);
        }

        return allowed.AsReadOnly();
    }
    
    public bool CanAccessEntity(string? userId, string entityName)
    {
        var policy = _configService.GetPolicy();

        if (!policy.AccessControl.Enabled)
            return true;

        var roles = GetUserRoles(userId);
        if (roles.Count == 0)
            return false;
        
        foreach (var role in roles)
        {
            if (policy.AccessControl.Roles.TryGetValue(role, out var def) && def.AllowAll)
                return true;
        }
        
        if (!policy.Entities.TryGetValue(entityName, out var entityPolicy))
            return false;

        var allowedRoles = entityPolicy.Access.AllowedRoles;
        
        if (allowedRoles.Count == 0)
            return true;

        return allowedRoles.Any(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase));
    }
    
    public UnauthorizedBehavior GetUnauthorizedBehavior()
    {
        return _configService.GetPolicy().AccessControl.UnauthorizedBehavior;
    }
    
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
