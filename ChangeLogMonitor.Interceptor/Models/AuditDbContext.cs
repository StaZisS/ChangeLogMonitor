using Microsoft.EntityFrameworkCore;

namespace ChangeLogMonitor.Interceptor.Models;

/// <summary>
///     DbContext для работы с таблицей audit_log
/// </summary>
public class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

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

            // Индексы
            entity.HasIndex(e => e.TransactionId)
                .HasDatabaseName("idx_audit_log_transaction_id");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_audit_log_created_at");

            entity.HasIndex(e => e.ProcessedAt)
                .HasDatabaseName("idx_audit_log_processed_at");
        });
    }
}