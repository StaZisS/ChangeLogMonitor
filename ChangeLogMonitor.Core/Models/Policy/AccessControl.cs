using ChangeLogMonitor.Core.Enums;

namespace ChangeLogMonitor.Core.Models.Policy;

/// <summary>
///     Настройки контроля доступа к аудит-логам
/// </summary>
public class AccessControl
{
    /// <summary>
    ///     Включен ли контроль доступа
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    ///     Поведение при неавторизованном доступе
    /// </summary>
    public UnauthorizedBehavior UnauthorizedBehavior { get; set; } = UnauthorizedBehavior.Deny;

    /// <summary>
    ///     Определения ролей
    /// </summary>
    public Dictionary<string, RoleDefinition> Roles { get; set; } = new();

    /// <summary>
    ///     Маппинг userId -> роли
    /// </summary>
    public Dictionary<string, UserRoleMapping> Users { get; set; } = new();

    /// <summary>
    ///     Роли по умолчанию для пользователей, не указанных в маппинге
    /// </summary>
    public List<string> DefaultRoles { get; set; } = new();

    /// <summary>
    ///     Разрешен ли анонимный доступ
    /// </summary>
    public bool AllowAnonymous { get; set; } = false;

    /// <summary>
    ///     Роли для анонимных пользователей
    /// </summary>
    public List<string> AnonymousRoles { get; set; } = new();
}

/// <summary>
///     Определение роли
/// </summary>
public class RoleDefinition
{
    /// <summary>
    ///     Описание роли
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Полный доступ ко всем сущностям (обход проверок)
    /// </summary>
    public bool AllowAll { get; set; } = false;
}

/// <summary>
///     Маппинг ролей для пользователя
/// </summary>
public class UserRoleMapping
{
    /// <summary>
    ///     Список ролей пользователя
    /// </summary>
    public List<string> Roles { get; set; } = new();
}
