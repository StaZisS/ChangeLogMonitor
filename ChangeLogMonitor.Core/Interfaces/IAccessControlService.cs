using ChangeLogMonitor.Core.Enums;

namespace ChangeLogMonitor.Core.Interfaces;

/// <summary>
///     Сервис контроля доступа к аудит-логам.
///     Определяет, какие сущности доступны пользователю на основе его ролей.
/// </summary>
public interface IAccessControlService
{
    /// <summary>
    ///     Включен ли контроль доступа
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Получает роли пользователя по его идентификатору.
    ///     Для анонимных пользователей возвращает AnonymousRoles или пустой список.
    ///     Для пользователей без явного маппинга возвращает DefaultRoles.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя (из JWT)</param>
    /// <returns>Список ролей пользователя</returns>
    IReadOnlyList<string> GetUserRoles(string? userId);

    /// <summary>
    ///     Получает список сущностей, доступных для указанных ролей
    /// </summary>
    /// <param name="roles">Список ролей</param>
    /// <returns>Список имен сущностей</returns>
    IReadOnlyList<string> GetAllowedEntities(IEnumerable<string> roles);

    /// <summary>
    ///     Проверяет, может ли пользователь просматривать аудит-логи сущности
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="entityName">Имя сущности</param>
    /// <returns>true если доступ разрешен</returns>
    bool CanAccessEntity(string? userId, string entityName);

    /// <summary>
    ///     Получает настроенное поведение для неавторизованного доступа
    /// </summary>
    UnauthorizedBehavior GetUnauthorizedBehavior();

    /// <summary>
    ///     Проверяет, есть ли у пользователя роль с allowAll (полный доступ)
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <returns>true если у пользователя полный доступ</returns>
    bool HasFullAccess(string? userId);
}
