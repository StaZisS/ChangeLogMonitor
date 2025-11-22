using ChangeLogMonitor.Interceptor.Models;
using ChangeLogMonitor.Interceptor.Tests.TestEntities;
using Microsoft.EntityFrameworkCore;

namespace ChangeLogMonitor.Interceptor.Tests.Infrastructure;

/// <summary>
///     Вспомогательный DbContext, который объединяет схему audit_log и тестовые сущности.
///     Нужен только для первичной инициализации схемы БД в интеграционных тестах.
/// </summary>
internal class TestSchemaDbContext : DbContext
{
    public TestSchemaDbContext(DbContextOptions<TestSchemaDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_log");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.ProcessedAt)
                .HasColumnName("processed_at");

            entity.Property(e => e.TransactionId)
                .HasColumnName("transaction_id")
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Payload)
                .HasColumnName("payload")
                .IsRequired()
                .HasColumnType("bytea");

            entity.HasIndex(e => e.TransactionId)
                .HasDatabaseName("idx_audit_log_transaction_id");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_audit_log_created_at");

            entity.HasIndex(e => e.ProcessedAt)
                .HasDatabaseName("idx_audit_log_processed_at");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<SessionCache>(entity =>
        {
            entity.ToTable("session_caches");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(100);
        });
    }
}