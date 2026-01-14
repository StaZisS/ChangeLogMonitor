namespace ChangeLogMonitor.Integration.Tests.TestEntities;

/// <summary>
///     Товар для тестирования
/// </summary>
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsAvailable { get; set; }
}
