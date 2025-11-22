using System.ComponentModel.DataAnnotations;

namespace ChangeLogMonitor.TestHarness.Domain;

public class User
{
    public Guid Id { get; set; }

    [MaxLength(64)] public required string Username { get; set; }

    [MaxLength(255)] public required string PasswordHash { get; set; }

    [MaxLength(128)] public required string FullName { get; set; }

    [MaxLength(256)] public required string Email { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}