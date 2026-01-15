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

        // Создаём таблицы Tags и OrderTags если их нет (для существующих БД)
        await EnsureTagsTablesAsync(context, cancellationToken);

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

        // Seed Tags for testing M2M collection changes
        if (!await context.Tags.AnyAsync(t => t.Id == SeedData.UrgentTagId, cancellationToken))
        {
            context.Tags.AddRange(
                new Tag { Id = SeedData.UrgentTagId, Name = "Urgent", Color = "#ff0000" },
                new Tag { Id = SeedData.VipTagId, Name = "VIP", Color = "#ffd700" },
                new Tag { Id = SeedData.DiscountTagId, Name = "Discount", Color = "#00ff00" },
                new Tag { Id = SeedData.NewCustomerTagId, Name = "New Customer", Color = "#0000ff" },
                new Tag { Id = SeedData.RepeatOrderTagId, Name = "Repeat Order", Color = "#800080" }
            );
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

    /// <summary>
    ///     Создаёт таблицы Tags и OrderTags если их нет (для существующих БД)
    /// </summary>
    private static async Task EnsureTagsTablesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var isPostgres = connection.GetType().Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);

        if (!isPostgres)
            return;

        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            // Проверяем существует ли таблица Tags
            await using var checkTagsCmd = connection.CreateCommand();
            checkTagsCmd.CommandText = @"
                SELECT COUNT(*) FROM information_schema.tables
                WHERE table_name = 'Tags' AND table_schema = 'public'";

            var tagsExists = Convert.ToInt32(await checkTagsCmd.ExecuteScalarAsync(cancellationToken)) > 0;

            if (!tagsExists)
            {
                await using var createTagsCmd = connection.CreateCommand();
                createTagsCmd.CommandText = @"
                    CREATE TABLE ""Tags"" (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""Name"" character varying(64) NOT NULL,
                        ""Color"" character varying(7)
                    );
                    CREATE UNIQUE INDEX ""IX_Tags_Name"" ON ""Tags"" (""Name"");
                ";
                await createTagsCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Проверяем существует ли таблица OrderTags
            await using var checkOrderTagsCmd = connection.CreateCommand();
            checkOrderTagsCmd.CommandText = @"
                SELECT COUNT(*) FROM information_schema.tables
                WHERE table_name = 'OrderTags' AND table_schema = 'public'";

            var orderTagsExists = Convert.ToInt32(await checkOrderTagsCmd.ExecuteScalarAsync(cancellationToken)) > 0;

            if (!orderTagsExists)
            {
                await using var createOrderTagsCmd = connection.CreateCommand();
                createOrderTagsCmd.CommandText = @"
                    CREATE TABLE ""OrderTags"" (
                        ""OrderId"" uuid NOT NULL,
                        ""TagId"" uuid NOT NULL,
                        ""AssignedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        PRIMARY KEY (""OrderId"", ""TagId""),
                        CONSTRAINT ""FK_OrderTags_Orders_OrderId"" FOREIGN KEY (""OrderId"") REFERENCES ""Orders"" (""Id"") ON DELETE CASCADE,
                        CONSTRAINT ""FK_OrderTags_Tags_TagId"" FOREIGN KEY (""TagId"") REFERENCES ""Tags"" (""Id"") ON DELETE CASCADE
                    );
                    CREATE INDEX ""IX_OrderTags_TagId"" ON ""OrderTags"" (""TagId"");
                ";
                await createOrderTagsCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch
        {
            // Игнорируем ошибки - возможно таблицы уже существуют или Orders ещё не создана
        }
    }
}