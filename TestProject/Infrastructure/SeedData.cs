namespace TestProject.Infrastructure;

internal static class SeedData
{
    public const string DefaultUsername = "demo-user";
    public const string DefaultPassword = "demo-pass";
    public static readonly Guid DefaultUserId = Guid.Parse("fe3eab31-1fc4-4c1f-b1f0-60bc78cb2a86");

    public const string SecondUsername = "test-user";
    public const string SecondPassword = "test-pass";
    public static readonly Guid SecondUserId = Guid.Parse("a1b2c3d4-5e6f-7a8b-9c0d-e1f2a3b4c5d6");

    // Tags for testing M2M collection changes
    public static readonly Guid UrgentTagId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid VipTagId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid DiscountTagId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid NewCustomerTagId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid RepeatOrderTagId = Guid.Parse("55555555-5555-5555-5555-555555555555");
}