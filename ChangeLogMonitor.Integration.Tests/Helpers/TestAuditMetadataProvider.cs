using ChangeLogMonitor.Interceptor.Services;

namespace ChangeLogMonitor.Integration.Tests.Helpers;

/// <summary>
///     Тестовая реализация IAuditMetadataProvider с настраиваемыми значениями
/// </summary>
public class TestAuditMetadataProvider : IAuditMetadataProvider
{
    public string UserId { get; set; } = "test-user-id";
    public string UserName { get; set; } = "Test User";
    public string RequestId { get; set; } = "test-request-id";
    public string? ServiceName { get; set; } = "IntegrationTests";
    public string? ClientIp { get; set; } = "127.0.0.1";
    public string? UserAgent { get; set; } = "TestRunner/1.0";
    public Dictionary<string, string> Hints { get; set; } = new();

    public string GetUserId() => UserId;
    public string GetUserName() => UserName;
    public string? GetRequestId() => RequestId;
    public string? GetServiceName() => ServiceName;
    public string? GetClientIp() => ClientIp;
    public string? GetUserAgent() => UserAgent;
    public Dictionary<string, string>? GetHints() => Hints;

    public void Reset()
    {
        UserId = "test-user-id";
        UserName = "Test User";
        RequestId = "test-request-id";
        ServiceName = "IntegrationTests";
        ClientIp = "127.0.0.1";
        UserAgent = "TestRunner/1.0";
        Hints = new Dictionary<string, string>();
    }

    public void SetUser(string userId, string userName)
    {
        UserId = userId;
        UserName = userName;
    }

    public void AddHint(string key, string value)
    {
        Hints[key] = value;
    }
}
