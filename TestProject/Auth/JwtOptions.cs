namespace TestProject.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "TestProject";
    public string Audience { get; set; } = "TestProject.Clients";
    public string SigningKey { get; set; } = "local-signing-key-change-me";
    public int ExpiryMinutes { get; set; } = 900000;
}