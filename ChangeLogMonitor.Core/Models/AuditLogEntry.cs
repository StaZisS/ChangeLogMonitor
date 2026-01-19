using ChangeLogMonitor.Core.Enums;

namespace ChangeLogMonitor.Core.Models;

/// <summary>
///     Основная сущность лога аудита
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public AuditOperationType OperationType { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public Dictionary<string, string> Context { get; set; } = new();

    public List<FieldChange> FieldChanges { get; set; } = new();

    public List<ReferenceChange> ReferenceChanges { get; set; } = new();

    public List<CollectionChange> CollectionChanges { get; set; } = new();

    public bool IsNormalized { get; set; }

    public DateTime? NormalizedAt { get; set; }

    public string? FinalMessage { get; set; }
}