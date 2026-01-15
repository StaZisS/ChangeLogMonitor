using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ChangeLogMonitor.Core.Attributes;

namespace ChangeLogMonitor.TestHarness.Domain;

/// <summary>
///     Статус заказа
/// </summary>
public enum OrderStatus
{
    [AuditEnumLabel("Новый")]
    New = 0,

    [AuditEnumLabel("В обработке")]
    Processing = 1,

    [AuditEnumLabel("Подтверждён")]
    Confirmed = 2,

    [AuditEnumLabel("Отправлен")]
    Shipped = 3,

    [AuditEnumLabel("Доставлен")]
    Delivered = 4,

    [AuditEnumLabel("Отменён")]
    Cancelled = 5
}

public class Order
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    [MaxLength(512)] public required string Description { get; set; }

    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.New;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<OrderTag> OrderTags { get; set; } = new List<OrderTag>();
}