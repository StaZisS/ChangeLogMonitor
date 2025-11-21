using System.Linq;
using System.Text.Json;

namespace ChangeLogMonitor.DataAggregator.Processing;

internal static class TxBucketSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(TxBucket bucket)
    {
        var document = new BucketDocument
        {
            TxId = bucket.TxId,
            CreatedAtUnixMs = bucket.CreatedAtUnixMs,
            UpdatedAtUnixMs = bucket.UpdatedAtUnixMs,
            ExpectedTotal = bucket.ExpectedTotal,
            ExpectedByTable = bucket.ExpectedByTable != null
                ? new Dictionary<string, int>(bucket.ExpectedByTable, StringComparer.OrdinalIgnoreCase)
                : null,
            ReceivedByTable = new Dictionary<string, int>(bucket.ReceivedByTable, StringComparer.OrdinalIgnoreCase),
            ReceivedTotal = bucket.ReceivedTotal,
            Ordering = bucket.Ordering,
            MetaJson = bucket.MetaJson,
            ExceededMaxEvents = bucket.ExceededMaxEvents,
            HasMetadata = bucket.HasMetadata,
            EventIds = bucket.EventIds.ToList(),
            Events = bucket.Events.Select(e => new BucketEventDocument
            {
                Table = e.Table,
                Operation = e.Operation,
                TimestampMs = e.TimestampMs,
                TotalOrder = e.TotalOrder,
                PayloadJson = e.PayloadJson,
                EventUid = e.EventUid
            }).ToList()
        };

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    public static TxBucket Deserialize(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new JsonException("Bucket payload is empty");
        }

        var document = JsonSerializer.Deserialize<BucketDocument>(rawJson, SerializerOptions)
                       ?? throw new JsonException("Bucket payload is invalid");

        var bucket = new TxBucket
        {
            TxId = document.TxId,
            CreatedAtUnixMs = document.CreatedAtUnixMs,
            UpdatedAtUnixMs = document.UpdatedAtUnixMs,
            ExpectedTotal = document.ExpectedTotal,
            ExpectedByTable = document.ExpectedByTable != null
                ? new Dictionary<string, int>(document.ExpectedByTable, StringComparer.OrdinalIgnoreCase)
                : null,
            Ordering = document.Ordering,
            MetaJson = document.MetaJson,
            ExceededMaxEvents = document.ExceededMaxEvents,
            HasMetadata = document.HasMetadata,
            ReceivedTotal = document.ReceivedTotal
        };

        bucket.ReceivedByTable.Clear();
        if (document.ReceivedByTable != null)
        {
            foreach (var pair in document.ReceivedByTable)
            {
                bucket.ReceivedByTable[pair.Key] = pair.Value;
            }
        }

        bucket.EventIds.Clear();
        if (document.EventIds != null)
        {
            foreach (var id in document.EventIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    bucket.EventIds.Add(id);
                }
            }
        }

        bucket.Events.Clear();
        if (document.Events != null)
        {
            foreach (var e in document.Events)
            {
                bucket.Events.Add(new TxBucketEvent(
                    e.Table ?? string.Empty,
                    e.Operation ?? string.Empty,
                    e.TimestampMs,
                    e.TotalOrder,
                    string.IsNullOrWhiteSpace(e.PayloadJson) ? "{}" : e.PayloadJson,
                    e.EventUid ?? string.Empty));
            }
        }

        return bucket;
    }

    private sealed class BucketDocument
    {
        public string TxId { get; set; } = string.Empty;
        public long CreatedAtUnixMs { get; set; }
        public long UpdatedAtUnixMs { get; set; }
        public int? ExpectedTotal { get; set; }
        public Dictionary<string, int>? ExpectedByTable { get; set; }
        public Dictionary<string, int>? ReceivedByTable { get; set; }
        public int ReceivedTotal { get; set; }
        public List<BucketEventDocument>? Events { get; set; }
        public List<string>? EventIds { get; set; }
        public string? Ordering { get; set; }
        public string? MetaJson { get; set; }
        public bool ExceededMaxEvents { get; set; }
        public bool HasMetadata { get; set; }
    }

    private sealed class BucketEventDocument
    {
        public string? Table { get; set; }
        public string? Operation { get; set; }
        public long TimestampMs { get; set; }
        public long? TotalOrder { get; set; }
        public string? PayloadJson { get; set; }
        public string? EventUid { get; set; }
    }
}
