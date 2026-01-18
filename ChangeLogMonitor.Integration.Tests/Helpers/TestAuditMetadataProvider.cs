using ChangeLogMonitor.Interceptor.Services;

namespace ChangeLogMonitor.Integration.Tests.Helpers;

public class TestAuditMetadataProvider : IAuditMetadataProvider
{
    public string UserId { get; set; } = "test-user-id";
    public string UserName { get; set; } = "Test User";
    public string RequestId { get; set; } = "test-request-id";
    public string? ServiceName { get; set; } = "IntegrationTests";
    public string? ClientIp { get; set; } = "127.0.0.1";
    public string? UserAgent { get; set; } = "TestRunner/1.0";
    public Dictionary<string, string> Hints { get; set; } = new();

    public string GetUserId()
    {
        return UserId;
    }

    public string GetUserName()
    {
        return UserName;
    }

    public string? GetRequestId()
    {
        return RequestId;
    }

    public string? GetServiceName()
    {
        return ServiceName;
    }

    public string? GetClientIp()
    {
        return ClientIp;
    }

    public string? GetUserAgent()
    {
        return UserAgent;
    }

    public Dictionary<string, string>? GetHints()
    {
        return Hints;
    }

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