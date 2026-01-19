using ChangeLogMonitor.Core.Interfaces;

namespace ChangeLogMonitor.Finalization.Services;

/// <summary>
///     Test implementation of ICurrentUserService for development.
///     Allows specifying any userId without JWT token.
/// </summary>
public class TestCurrentUserService : ICurrentUserService
{
    private readonly string? _userId;

    public TestCurrentUserService(string? userId = null, bool isAuthenticated = true)
    {
        _userId = userId;
        IsAuthenticated = isAuthenticated && userId != null;
    }

    public string? GetUserId()
    {
        return _userId;
    }

    public bool IsAuthenticated { get; }

    public static TestCurrentUserService Admin(string userId = "admin-user-id")
    {
        return new TestCurrentUserService(userId);
    }

    public static TestCurrentUserService RegularUser(string userId = "regular-user-id")
    {
        return new TestCurrentUserService(userId);
    }

    public static TestCurrentUserService Anonymous()
    {
        return new TestCurrentUserService(null, false);
    }
}