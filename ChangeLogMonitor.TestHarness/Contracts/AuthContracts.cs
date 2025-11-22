namespace ChangeLogMonitor.TestHarness.Contracts;

public sealed record LoginRequest(string Username, string Password);

public sealed record TokenResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserSummary User);

public sealed record UserSummary(Guid Id, string Username, string FullName, string Email);