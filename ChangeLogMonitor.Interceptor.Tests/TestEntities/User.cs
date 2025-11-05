namespace ChangeLogMonitor.Interceptor.Tests.TestEntities;

/// <summary>
/// Тестовая сущность User для проверки работы интерцептора
/// </summary>
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
