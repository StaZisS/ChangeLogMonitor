namespace ChangeLogMonitor.TestHarness.Domain;

public class OrderTag
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid TagId { get; set; }
    public Tag? Tag { get; set; }

    public DateTimeOffset AssignedAt { get; set; }
}
