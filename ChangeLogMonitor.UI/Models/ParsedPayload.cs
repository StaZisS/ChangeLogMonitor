using ChangeLogMonitor.Core.Models;

namespace ChangeLogMonitor.UI.Models;

public class ParsedPayload
{
    public List<FieldChange> FieldChanges { get; set; } = new();
    public List<ReferenceChange> ReferenceChanges { get; set; } = new();
    public List<CollectionChange> CollectionChanges { get; set; } = new();
    public Dictionary<string, object>? Meta { get; set; }
    public bool IsIncomplete { get; set; }

    public bool HasAnyChanges =>
        FieldChanges.Count > 0 ||
        ReferenceChanges.Count > 0 ||
        CollectionChanges.Count > 0;
}
