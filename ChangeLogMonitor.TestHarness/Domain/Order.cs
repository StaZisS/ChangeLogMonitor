using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChangeLogMonitor.TestHarness.Domain;

public class Order
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    [MaxLength(512)]
    public required string Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
