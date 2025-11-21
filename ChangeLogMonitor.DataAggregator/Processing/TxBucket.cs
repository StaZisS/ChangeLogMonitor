using System.Text.Json;
using ChangeLogMonitor.DataAggregator.Models;

namespace ChangeLogMonitor.DataAggregator.Processing;

public sealed class TxBucket
{
    public string TxId { get; init; } = string.Empty;
    public long CreatedAtUnixMs { get; set; }
    public long UpdatedAtUnixMs { get; set; }
    public int? ExpectedTotal { get; set; }
    public Dictionary<string, int>? ExpectedByTable { get; set; }
    public Dictionary<string, int> ReceivedByTable { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int ReceivedTotal { get; internal set; }
    public List<TxBucketEvent> Events { get; } = new();
    public HashSet<string> EventIds { get; } = new(StringComparer.Ordinal);
    public string? Ordering { get; set; }
    public string? MetaJson { get; set; }
    public bool ExceededMaxEvents { get; internal set; }
    public bool HasMetadata { get; internal set; }

    public static TxBucket Create(string txId, long timestampMs) =>
        new()
        {
            TxId = txId,
            CreatedAtUnixMs = timestampMs,
            UpdatedAtUnixMs = timestampMs
        };

    public void Touch(long timestampMs)
    {
        UpdatedAtUnixMs = Math.Max(UpdatedAtUnixMs, timestampMs);
    }

    public TxBucketAddResult AddEvent(TxBucketEvent record, int maxEvents)
    {
        if (EventIds.Contains(record.EventUid))
        {
            return TxBucketAddResult.Duplicate;
        }

        if (Events.Count >= maxEvents)
        {
            ExceededMaxEvents = true;
            return TxBucketAddResult.LimitReached;
        }

        EventIds.Add(record.EventUid);
        Events.Add(record);

        if (!string.IsNullOrWhiteSpace(record.Table))
        {
            ReceivedByTable.TryGetValue(record.Table, out var count);
            ReceivedByTable[record.Table] = count + 1;
        }

        ReceivedTotal++;
        UpdatedAtUnixMs = Math.Max(UpdatedAtUnixMs, record.TimestampMs);
        return TxBucketAddResult.Added;
    }

    public bool ApplyMetadata(TxMetadata metadata)
    {
        var mutated = !HasMetadata;
        HasMetadata = true;

        if (metadata.ExpectedTotal.HasValue && ExpectedTotal != metadata.ExpectedTotal)
        {
            ExpectedTotal = metadata.ExpectedTotal;
            mutated = true;
        }

        if (metadata.ExpectedByTable != null)
        {
            var normalized = new Dictionary<string, int>(metadata.ExpectedByTable, StringComparer.OrdinalIgnoreCase);
            if (ExpectedByTable == null || !DictionaryEquals(ExpectedByTable, normalized))
            {
                ExpectedByTable = normalized;
                mutated = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(metadata.Ordering))
        {
            var trimmed = metadata.Ordering.Trim();
            if (!string.Equals(trimmed, Ordering, StringComparison.OrdinalIgnoreCase))
            {
                Ordering = trimmed;
                mutated = true;
            }
        }

        if (metadata.Meta.HasValue)
        {
            var meta = metadata.Meta.Value.GetRawText();
            if (!string.Equals(MetaJson, meta, StringComparison.Ordinal))
            {
                MetaJson = meta;
                mutated = true;
            }
        }

        if (mutated)
        {
            UpdatedAtUnixMs = Math.Max(UpdatedAtUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        return mutated;
    }

    public bool IsComplete()
    {
        var byTotal = ExpectedTotal.HasValue && ReceivedTotal == ExpectedTotal.Value;
        var byTable = ExpectedByTable != null && ExpectedByTable.Count > 0 &&
                      ExpectedByTable.All(kv =>
                          ReceivedByTable.TryGetValue(kv.Key, out var got) && got == kv.Value);
        return byTotal || byTable;
    }

    public JsonElement? MetaAsJson()
    {
        if (string.IsNullOrWhiteSpace(MetaJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(MetaJson);
        return document.RootElement.Clone();
    }

    private static bool DictionaryEquals(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) || value != pair.Value)
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record TxBucketEvent(
    string Table,
    string Operation,
    long TimestampMs,
    long? TotalOrder,
    string PayloadJson,
    string EventUid)
{
    public JsonElement PayloadAsJson()
    {
        using var document = JsonDocument.Parse(PayloadJson);
        return document.RootElement.Clone();
    }
}

public enum TxBucketAddResult
{
    Added,
    Duplicate,
    LimitReached
}
