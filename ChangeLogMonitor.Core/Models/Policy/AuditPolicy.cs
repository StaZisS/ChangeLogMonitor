using ChangeLogMonitor.Core.Enums;

namespace ChangeLogMonitor.Core.Models.Policy;

/// <summary>
/// Главная политика аудита для всего приложения
/// </summary>
public class AuditPolicy
{
    public string Version { get; set; } = "1.0";

    public AuditMode Mode { get; set; } = AuditMode.Whitelist;

    public CreateBehavior OnCreate { get; set; } = CreateBehavior.EventOnly;

    public UpdateBehavior OnUpdate { get; set; } = UpdateBehavior.Delta;

    public DeleteBehavior OnDelete { get; set; } = DeleteBehavior.EventOnly;

    // Глобальные исключения (поля, которые никогда не логируются)
    public List<string> GlobalFieldExclusions { get; set; } = new();

    // Пресеты для методов
    public Dictionary<string, MaskPreset> MaskPresets { get; set; } = new();

    public Dictionary<string, HashPreset> HashPresets { get; set; } = new();

    public Dictionary<string, EncryptPreset> EncryptPresets { get; set; } = new();

    // Пресеты для ссылок и коллекций
    public Dictionary<string, ReferencePreset> ReferencePresets { get; set; } = new();

    public Dictionary<string, CollectionPreset> CollectionPresets { get; set; } = new();

    // Умолчания
    public ReferenceDefaults ReferenceDefaults { get; set; } = new();

    public CollectionDefaults CollectionDefaults { get; set; } = new();

    // Политики для конкретных сущностей
    public Dictionary<string, EntityPolicy> Entities { get; set; } = new();

    // Локализация
    public string DefaultCulture { get; set; } = "ru-RU";

    public string DefaultTimeZone { get; set; } = "Asia/Almaty";
}
