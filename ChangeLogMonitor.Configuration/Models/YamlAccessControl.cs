using YamlDotNet.Serialization;

namespace ChangeLogMonitor.Configuration.Models;

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

public class YamlRoleDefinition
{
    [YamlMember(Alias = "description")] public string? Description { get; set; }

    [YamlMember(Alias = "allowAll")] public bool AllowAll { get; set; } = false;
}

public class YamlUserRoles
{
    [YamlMember(Alias = "roles")] public List<string> Roles { get; set; } = new();
}

public class YamlEntityAccess
{
    [YamlMember(Alias = "allowedRoles")] public List<string>? AllowedRoles { get; set; }
}
