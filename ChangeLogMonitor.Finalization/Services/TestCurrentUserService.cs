using ChangeLogMonitor.Core.Interfaces;

namespace ChangeLogMonitor.Finalization.Services;

/// <summary>
/// Test implementation of ICurrentUserService for unit tests and development.
/// Allows specifying any userId without JWT token.
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
    /// Creates a service for admin user
    /// </summary>
    public static TestCurrentUserService Admin(string userId = "admin-user-id") =>
        new(userId);

    /// <summary>
    /// Creates a service for regular user
    /// </summary>
    public static TestCurrentUserService RegularUser(string userId = "regular-user-id") =>
        new(userId);

    /// <summary>
    /// Creates a service for anonymous user
    /// </summary>
    public static TestCurrentUserService Anonymous() =>
        new(null, false);
}
