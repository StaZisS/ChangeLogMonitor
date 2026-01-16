using ChangeLogMonitor.UI.Models;

namespace ChangeLogMonitor.UI.Services;

public interface IAuditLogViewService
{
    Task<AuditLogListViewModel> GetLogsAsync(
        AuditLogFilterViewModel filter,
        string? cursor,
        int limit,
        string timezone,
        CancellationToken cancellationToken);

    ParsedPayload ParsePayload(string json);

    /// <summary>
    ///     Получает список сущностей, доступных текущему пользователю
    /// </summary>
    IReadOnlyList<string> GetAllowedEntities();

    /// <summary>
    ///     Проверяет, включен ли контроль доступа
    /// </summary>
    bool IsAccessControlEnabled { get; }

    /// <summary>
    ///     Получает список всех настроенных сущностей с их display names.
    ///     Если включен контроль доступа - возвращает только доступные пользователю сущности.
    /// </summary>
    IReadOnlyList<EntityInfo> GetAvailableEntities();
}
