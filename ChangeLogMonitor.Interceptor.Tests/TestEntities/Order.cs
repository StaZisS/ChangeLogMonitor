namespace ChangeLogMonitor.Interceptor.Tests.TestEntities;

/// <summary>
///     Тестовая сущность Order для проверки работы интерцептора
/// </summary>
public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime OrderDate { get; set; }
}