namespace ChangeLogMonitor.Core.Models.Policy;

/// <summary>
///     Настройки доступа к аудит-логам конкретной сущности
/// </summary>
public class EntityAccess
{
    /// <summary>
    ///     Роли, которым разрешен доступ к аудит-логам этой сущности.
    ///     Пустой список означает доступ для всех ролей.
    /// </summary>
    public List<string> AllowedRoles { get; set; } = new();
}
