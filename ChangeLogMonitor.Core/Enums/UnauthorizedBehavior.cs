namespace ChangeLogMonitor.Core.Enums;

/// <summary>
///     Поведение при неавторизованном доступе к аудит-логам
/// </summary>
public enum UnauthorizedBehavior
{
    /// <summary>
    ///     Отклонить запрос с кодом 403 Forbidden
    /// </summary>
    Deny,

    /// <summary>
    ///     Вернуть пустой результат (тихая фильтрация)
    /// </summary>
    Filter
}
