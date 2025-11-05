namespace ChangeLogMonitor.Interceptor.Tests.TestEntities;

/// <summary>
/// Тестовая сущность SessionCache - используется для проверки исключения из аудита
/// </summary>
public class SessionCache
{
    public int Id { get; set; }
    public string SessionId { get; set; } = null!;
    public string Data { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}
