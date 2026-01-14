using ChangeLogMonitor.TestHarness.Domain;
using ChangeLogMonitor.TestHarness.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ChangeLogMonitor.TestHarness.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.EnsureCreatedAsync(cancellationToken);

        var hasChanges = false;

        if (!await context.Users.AnyAsync(u => u.Id == SeedData.DefaultUserId, cancellationToken))
        {
            context.Users.Add(new User
            {
                Id = SeedData.DefaultUserId,
                Username = SeedData.DefaultUsername,
                PasswordHash = PasswordHasher.Hash(SeedData.DefaultPassword),
                FullName = "Demo Operator",
                Email = "demo@example.com",
                CreatedAt = DateTimeOffset.UtcNow
            });
            hasChanges = true;
        }

        if (!await context.Users.AnyAsync(u => u.Id == SeedData.SecondUserId, cancellationToken))
        {
            context.Users.Add(new User
            {
                Id = SeedData.SecondUserId,
                Username = SeedData.SecondUsername,
                PasswordHash = PasswordHasher.Hash(SeedData.SecondPassword),
                FullName = "Test User",
                Email = "test@example.com",
                CreatedAt = DateTimeOffset.UtcNow
            });
            hasChanges = true;
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}