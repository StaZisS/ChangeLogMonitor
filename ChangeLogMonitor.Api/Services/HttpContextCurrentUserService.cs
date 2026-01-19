using System.Security.Claims;
using ChangeLogMonitor.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ChangeLogMonitor.Api.Services;

/// <summary>
///     Implementation of ICurrentUserService that extracts user information from HttpContext.
///     Supports JWT claims and fallback to X-User-Id header for testing.
/// </summary>
public class HttpContextCurrentUserService : ICurrentUserService
{
    private const string UserIdHeader = "X-User-Id";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }
    
    public string? GetUserId()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
            return null;
        
        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var claimUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? user.FindFirstValue("sub")
                              ?? user.FindFirstValue("user_id")
                              ?? user.FindFirstValue("uid");
            if (!string.IsNullOrWhiteSpace(claimUserId))
                return claimUserId;
        }
        
        if (context.Request.Headers.TryGetValue(UserIdHeader, out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue))
            return headerValue.ToString();

        return null;
    }
    
    public bool IsAuthenticated =>
        GetUserId() != null;
}