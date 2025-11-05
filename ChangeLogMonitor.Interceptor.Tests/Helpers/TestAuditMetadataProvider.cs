using ChangeLogMonitor.Interceptor.Services;

namespace ChangeLogMonitor.Interceptor.Tests.Helpers;

/// <summary>
/// Тестовый провайдер метаданных с фиксированными значениями
/// </summary>
public class TestAuditMetadataProvider : IAuditMetadataProvider
{
    public string UserId { get; set; } = "test-user-123";
    public string UserName { get; set; } = "Test User";
    public string? RequestId { get; set; } = "req-test-456";
    public string? ServiceName { get; set; } = "TestService";
    public string? ClientIp { get; set; } = "127.0.0.1";
    public string? UserAgent { get; set; } = "TestAgent/1.0";
    public Dictionary<string, string>? Hints { get; set; }

    public string GetUserId() => UserId;
    public string GetUserName() => UserName;
    public string? GetRequestId() => RequestId;
    public string? GetServiceName() => ServiceName;
    public string? GetClientIp() => ClientIp;
    public string? GetUserAgent() => UserAgent;
    public Dictionary<string, string>? GetHints() => Hints;

    /// <summary>
    /// Сбрасывает все значения к значениям по умолчанию
    /// </summary>
    public void Reset()
    {
        UserId = "test-user-123";
        UserName = "Test User";
        RequestId = "req-test-456";
        ServiceName = "TestService";
        ClientIp = "127.0.0.1";
        UserAgent = "TestAgent/1.0";
        Hints = null;
    }
}
