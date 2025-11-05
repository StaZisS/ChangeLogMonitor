namespace ChangeLogMonitor.Interceptor.Services;

/// <summary>
/// Интерфейс провайдера метаданных для аудита
/// Реализуется на стороне приложения для предоставления контекстной информации
/// </summary>
public interface IAuditMetadataProvider
{
    /// <summary>
    /// Получает ID текущего пользователя
    /// </summary>
    string GetUserId();

    /// <summary>
    /// Получает имя текущего пользователя
    /// </summary>
    string GetUserName();

    /// <summary>
    /// Получает ID запроса/трассировки (опционально)
    /// </summary>
    string? GetRequestId();

    /// <summary>
    /// Получает название сервиса (опционально)
    /// </summary>
    string? GetServiceName();

    /// <summary>
    /// Получает IP адрес клиента (опционально)
    /// </summary>
    string? GetClientIp();

    /// <summary>
    /// Получает User-Agent клиента (опционально)
    /// </summary>
    string? GetUserAgent();

    /// <summary>
    /// Получает дополнительные подсказки/метки (опционально)
    /// </summary>
    Dictionary<string, string>? GetHints();
}
