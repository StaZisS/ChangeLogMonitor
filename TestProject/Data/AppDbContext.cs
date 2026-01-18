using Microsoft.EntityFrameworkCore;
using TestProject.Domain;

namespace TestProject.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<OrderTag> OrderTags => Set<OrderTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Amount).HasPrecision(18, 2);
            entity.Property(o => o.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<OrderTag>(entity =>
        {
            entity.HasKey(ot => new { ot.OrderId, ot.TagId });
            entity.Property(ot => ot.AssignedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(ot => ot.Order)
                .WithMany(o => o.OrderTags)
                .HasForeignKey(ot => ot.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ot => ot.Tag)
                .WithMany(t => t.OrderTags)
                .HasForeignKey(ot => ot.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}