using System.Security.Cryptography;
using System.Text;

namespace ChangeLogMonitor.TestHarness.Infrastructure;

internal static class PasswordHasher
{
    public static string Hash(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        using var sha256 = SHA256.Create();
        var hashed = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hashed);
    }

    public static bool Verify(string value, string hash) =>
        Hash(value) == hash;
}
