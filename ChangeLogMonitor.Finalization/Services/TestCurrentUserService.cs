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
    
    public string? GetUserId() => _userId;
    
    public bool IsAuthenticated => _isAuthenticated;
    
    public static TestCurrentUserService Admin(string userId = "admin-user-id") =>
        new(userId);
    
    public static TestCurrentUserService RegularUser(string userId = "regular-user-id") =>
        new(userId);

    public static TestCurrentUserService Anonymous() =>
        new(null, false);
}
