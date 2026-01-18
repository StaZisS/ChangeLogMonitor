using System.Security.Claims;
using ChangeLogMonitor.Interceptor.Services;

namespace TestProject.Infrastructure;

public class HttpContextAuditMetadataProvider : IAuditMetadataProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextAuditMetadataProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return "anonymous";

        return user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("sub")
               ?? "unknown";
    }

    public string GetUserName()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return "Anonymous";

        return user.FindFirstValue(ClaimTypes.Name)
               ?? user.FindFirstValue("name")
               ?? user.Identity?.Name
               ?? "Unknown";
    }

    public string? GetRequestId()
    {
        return _httpContextAccessor.HttpContext?.TraceIdentifier;
    }

    public string? GetServiceName()
    {
        return "TestProject";
    }

    public string? GetClientIp()
    {
        return _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    public string? GetUserAgent()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
    }

    public Dictionary<string, string>? GetHints()
    {
        return null;
    }
}
