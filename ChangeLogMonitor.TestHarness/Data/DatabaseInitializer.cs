using ChangeLogMonitor.TestHarness.Domain;
using ChangeLogMonitor.TestHarness.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ChangeLogMonitor.TestHarness.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.EnsureCreatedAsync(cancellationToken);

        if (await context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        context.Users.Add(new User
        {
            Id = SeedData.DefaultUserId,
            Username = SeedData.DefaultUsername,
            PasswordHash = PasswordHasher.Hash(SeedData.DefaultPassword),
            FullName = "Demo Operator",
            Email = "demo@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
