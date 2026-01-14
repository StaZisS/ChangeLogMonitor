namespace ChangeLogMonitor.TestHarness.Infrastructure;

internal static class SeedData
{
    public const string DefaultUsername = "demo-user";
    public const string DefaultPassword = "demo-pass";
    public static readonly Guid DefaultUserId = Guid.Parse("fe3eab31-1fc4-4c1f-b1f0-60bc78cb2a86");

    public const string SecondUsername = "test-user";
    public const string SecondPassword = "test-pass";
    public static readonly Guid SecondUserId = Guid.Parse("a1b2c3d4-5e6f-7a8b-9c0d-e1f2a3b4c5d6");
}