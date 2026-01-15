using YamlDotNet.Serialization;

namespace ChangeLogMonitor.Configuration.Models;

/// <summary>
///     Настройки контроля доступа (YAML)
/// </summary>
public class YamlAccessControl
{
    [YamlMember(Alias = "enabled")] public bool Enabled { get; set; } = false;

    [YamlMember(Alias = "unauthorizedBehavior")]
    public string? UnauthorizedBehavior { get; set; }

    [YamlMember(Alias = "roles")] public Dictionary<string, YamlRoleDefinition>? Roles { get; set; }

    [YamlMember(Alias = "users")] public Dictionary<string, YamlUserRoles>? Users { get; set; }

    [YamlMember(Alias = "defaultRoles")] public List<string>? DefaultRoles { get; set; }

    [YamlMember(Alias = "allowAnonymous")] public bool AllowAnonymous { get; set; } = false;

    [YamlMember(Alias = "anonymousRoles")] public List<string>? AnonymousRoles { get; set; }
}

/// <summary>
///     Определение роли (YAML)
/// </summary>
public class YamlRoleDefinition
{
    [YamlMember(Alias = "description")] public string? Description { get; set; }

    [YamlMember(Alias = "allowAll")] public bool AllowAll { get; set; } = false;
}

/// <summary>
///     Маппинг ролей для пользователя (YAML)
/// </summary>
public class YamlUserRoles
{
    [YamlMember(Alias = "roles")] public List<string> Roles { get; set; } = new();
}

/// <summary>
///     Настройки доступа к сущности (YAML)
/// </summary>
public class YamlEntityAccess
{
    [YamlMember(Alias = "allowedRoles")] public List<string>? AllowedRoles { get; set; }
}
