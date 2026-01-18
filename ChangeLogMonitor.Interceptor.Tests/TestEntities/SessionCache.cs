namespace ChangeLogMonitor.Interceptor.Tests.TestEntities;

public class SessionCache
{
    public int Id { get; set; }
    public string SessionId { get; set; } = null!;
    public string Data { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}