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

        // Добавляем колонку Status если её нет (для существующих БД)
        await EnsureStatusColumnAsync(context, cancellationToken);

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

    /// <summary>
    ///     Добавляет колонку Status в таблицу Orders если её нет (для существующих БД)
    /// </summary>
    private static async Task EnsureStatusColumnAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var isPostgres = connection.GetType().Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);

        if (!isPostgres)
            return;

        try
        {
            await connection.OpenAsync(cancellationToken);

            // Проверяем существует ли колонка Status
            await using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = @"
                SELECT COUNT(*) FROM information_schema.columns
                WHERE table_name = 'Orders' AND column_name = 'Status'";

            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0;

            if (!exists)
            {
                await using var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = @"ALTER TABLE ""Orders"" ADD COLUMN ""Status"" integer NOT NULL DEFAULT 0";
                await alterCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch
        {
            // Игнорируем ошибки - возможно таблица ещё не создана
        }
    }
}