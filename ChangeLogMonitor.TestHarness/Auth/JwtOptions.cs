namespace ChangeLogMonitor.TestHarness.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "ChangeLogMonitor.TestHarness";
    public string Audience { get; set; } = "ChangeLogMonitor.TestHarness.Clients";
    public string SigningKey { get; set; } = "local-signing-key-change-me";
    public int ExpiryMinutes { get; set; } = 900000;
}
