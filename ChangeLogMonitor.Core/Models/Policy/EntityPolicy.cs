using ChangeLogMonitor.Core.Enums;

namespace ChangeLogMonitor.Core.Models.Policy;

/// <summary>
/// Политика аудита для конкретной сущности
/// </summary>
public class EntityPolicy
{
    public bool Enabled { get; set; } = true;

    public CreateBehavior? OnCreate { get; set; }

    public UpdateBehavior? OnUpdate { get; set; }

    public DeleteBehavior? OnDelete { get; set; }

    // Настройки полей
    public Dictionary<string, FieldPolicy> Fields { get; set; } = new();

    // Настройки ссылок (FK)
    public Dictionary<string, ReferencePolicy> References { get; set; } = new();

    // Настройки коллекций
    public Dictionary<string, CollectionPolicy> Collections { get; set; } = new();
}
