using System.Security.Claims;
using ChangeLogMonitor.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ChangeLogMonitor.Finalization.Services;

/// <summary>
///     Реализация ICurrentUserService, извлекающая информацию о пользователе из HttpContext.
///     Поддерживает JWT claims и fallback на header X-User-Id для тестирования.
/// </summary>
public class HttpContextCurrentUserService : ICurrentUserService
{
    private const string UserIdHeader = "X-User-Id";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    public string? GetUserId()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
            return null;

        // 1. Сначала пробуем JWT claims
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

        // 2. Fallback на header X-User-Id (для тестирования без JWT)
        if (context.Request.Headers.TryGetValue(UserIdHeader, out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString();
        }

        return null;
    }

    /// <inheritdoc />
    public bool IsAuthenticated
    {
        get
        {
            // Аутентифицирован если есть JWT или header X-User-Id
            return GetUserId() != null;
        }
    }
}

/// <summary>
///     Тестовая реализация ICurrentUserService для unit-тестов и разработки.
///     Позволяет подставить любой userId без JWT токена.
/// </summary>
public class TestCurrentUserService : ICurrentUserService
{
    private readonly string? _userId;
    private readonly bool _isAuthenticated;

    public TestCurrentUserService(string? userId = null, bool isAuthenticated = true)
    {
        _userId = userId;
        _isAuthenticated = isAuthenticated && userId != null;
    }

    /// <inheritdoc />
    public string? GetUserId() => _userId;

    /// <inheritdoc />
    public bool IsAuthenticated => _isAuthenticated;

    /// <summary>
    ///     Создает сервис для пользователя с ролью admin
    /// </summary>
    public static TestCurrentUserService Admin(string userId = "admin-user-id") =>
        new(userId);

    /// <summary>
    ///     Создает сервис для обычного пользователя
    /// </summary>
    public static TestCurrentUserService RegularUser(string userId = "regular-user-id") =>
        new(userId);

    /// <summary>
    ///     Создает сервис для анонимного пользователя
    /// </summary>
    public static TestCurrentUserService Anonymous() =>
        new(null, false);
}
