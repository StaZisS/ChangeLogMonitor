namespace ChangeLogMonitor.Core.Interfaces;

/// <summary>
///     Сервис для получения информации о текущем пользователе.
///     Извлекает user_id из JWT токена или другого источника аутентификации.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    ///     Проверяет, аутентифицирован ли текущий пользователь
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    ///     Получает идентификатор текущего пользователя из JWT claim "sub" или "nameidentifier".
    ///     Возвращает null для анонимных пользователей.
    /// </summary>
    string? GetUserId();
}