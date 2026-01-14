using System.Text.Json;
using ChangeLogMonitor.DataAggregator.Models;
using Microsoft.Extensions.Logging;
using Streamiz.Kafka.Net.Processors.Public;
using Streamiz.Kafka.Net.State;

namespace ChangeLogMonitor.DataAggregator.Processing;

internal sealed class TxAggregatorTransformer : ITransformer<string, string, string, string>
{
    private static readonly JsonSerializerOptions MetadataSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<TxAggregatorTransformer> _logger;
    private readonly TxAggregatorMetrics _metrics;

    private readonly TxAggregatorOptions _options;

    private ProcessorContext<string, string>? _context;
    private IReadOnlyKeyValueStore<string, string>? _metadataStore;
    private IReadOnlyKeyValueStore<string, string>? _readOnlyStateStore;
    private IKeyValueStore<string, string>? _stateStore;

    public TxAggregatorTransformer()
    {
        var (options, loggerFactory, metrics) = TxAggregatorTransformerDependencies.Resolve();
        _options = options;
        _metrics = metrics;
        _logger = loggerFactory.CreateLogger<TxAggregatorTransformer>();
    }

    public void Init(ProcessorContext<string, string> context)
    {
        _context = context;
        _stateStore = (IKeyValueStore<string, string>)context.GetStateStore(StoreNames.TxStateStore);
        _readOnlyStateStore = context.GetStateStore(StoreNames.TxStateStore) as IReadOnlyKeyValueStore<string, string>
                              ?? _stateStore;
        _metadataStore = context.GetStateStore(StoreNames.TxMetaStore) as IReadOnlyKeyValueStore<string, string>;
        context.Schedule(TimeSpan.FromMilliseconds(_options.FlushIntervalMs), PunctuationType.PROCESSING_TIME,
            OnPunctuate);
    }

    public Record<string, string>? Process(Record<string, string> record)
    {
        if (record.Value == null || string.IsNullOrWhiteSpace(record.Key))
        {
            _metrics.MarkMissingTxId();
            _metrics.MarkCdcEventProcessed();
            return null;
        }

        if (_context == null || _stateStore == null) throw new InvalidOperationException("Transformer not initialized");

        var partition = _context.Partition;
        var offset = _context.Offset;
        var topic = _context.Topic ?? string.Empty;
        var eventUid = $"{topic}:{partition}:{offset}";

        if (!TxCdcParser.TryParse(record.Value, record.Key, eventUid, out var bucketEvent))
        {
            _metrics.MarkParseFailure();
            _metrics.MarkCdcEventProcessed();
            _logger.LogWarning("Unable to parse CDC payload for tx {TxId}", record.Key);
            return null;
        }

        _metrics.MarkCdcEventProcessed();

        var bucket = LoadBucket(record.Key, bucketEvent.TimestampMs);
        var metadata = FetchMetadata(record.Key);
        var metadataPresent = metadata != null;
        var metadataApplied = metadataPresent && bucket.ApplyMetadata(metadata!);

        // CDC из audit_log трактуем как метаданные, а не как обычное событие
        if (string.Equals(bucketEvent.Table, "audit_log", StringComparison.OrdinalIgnoreCase))
            try
            {
                using var metaDoc = JsonDocument.Parse(bucketEvent.PayloadJson);
                var root = metaDoc.RootElement;

                // Extract actual payload from before/after structure if present
                JsonElement metaElement;
                if (root.TryGetProperty("after", out var afterEl) && afterEl.ValueKind == JsonValueKind.Object)
                {
                    metaElement = afterEl.Clone();
                }
                else if (root.TryGetProperty("before", out var beforeEl) && beforeEl.ValueKind == JsonValueKind.Object)
                {
                    metaElement = beforeEl.Clone();
                }
                else
                {
                    metaElement = root.Clone();
                }

                bucket.ApplyMetadata(new TxMetadata { Meta = metaElement });
                PersistBucket(record.Key, bucket);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse audit_log payload for tx {TxId}", record.Key);
                PersistBucket(record.Key, bucket);
                return null;
            }

        var addResult = bucket.AddEvent(bucketEvent, _options.MaxEventsPerBucket);
        if (addResult == TxBucketAddResult.Duplicate)
        {
            _metrics.MarkDuplicateEvent();
            if (metadataApplied) PersistBucket(record.Key, bucket);
            return null;
        }

        if (addResult == TxBucketAddResult.Added)
        {
            _metrics.AddBufferedEvent();
            _logger.LogInformation(
                "Buffered CDC event tx={TxId} table={Table} op={Op} topic={Topic} partition={Partition} offset={Offset} received={Received}/expected={Expected}",
                record.Key,
                bucketEvent.Table,
                bucketEvent.Operation,
                topic,
                partition,
                offset,
                bucket.ReceivedTotal,
                bucket.ExpectedTotal);
        }

        if (addResult == TxBucketAddResult.LimitReached)
        {
            _logger.LogWarning(
                "Max events reached for tx {TxId}. limit={Limit}, dropping record",
                record.Key,
                _options.MaxEventsPerBucket);

            if (_options.EmitPartialOnLimit)
            {
                var payload = EmitBucket(record.Key, bucket, BucketResolution.LimitExceeded);
                return Record<string, string>.Create(record.Key, payload);
            }

            PersistBucket(record.Key, bucket);
            return null;
        }

        if (bucket.IsComplete() && bucket.HasMetadata)
        {
            var payload = EmitBucket(record.Key, bucket, BucketResolution.Completed);
            return Record<string, string>.Create(record.Key, payload);
        }

        PersistBucket(record.Key, bucket);
        return null;
    }

    public void Close()
    {
        _context = null;
        _stateStore = null;
        _readOnlyStateStore = null;
        _metadataStore = null;
    }

    private void OnPunctuate(long timestamp)
    {
        if (_context == null || _stateStore == null || _readOnlyStateStore == null) return;

        var now = timestamp > 0 ? timestamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var updates = new List<KeyValuePair<string, string>>();
        var emissions = new List<(string Key, TxBucket Bucket, BucketResolution Resolution)>();

        using var enumerator = _readOnlyStateStore.All().GetEnumerator();
        while (enumerator.MoveNext())
        {
            var entry = enumerator.Current;
            if (string.IsNullOrWhiteSpace(entry.Value)) continue;

            TxBucket bucket;
            try
            {
                bucket = TxBucketSerializer.Deserialize(entry.Value);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Corrupted bucket state for tx {TxId}. Removing entry.", entry.Key);
                _stateStore.Delete(entry.Key);
                continue;
            }

            var metadata = FetchMetadata(entry.Key);
            var metadataChanged = metadata != null && bucket.ApplyMetadata(metadata);

            var expired = now - bucket.UpdatedAtUnixMs >= _options.HardTtlMs;
            var completed = bucket.IsComplete();
            var limitTriggered = bucket.ExceededMaxEvents && _options.EmitPartialOnLimit;

            if (completed || expired || limitTriggered)
            {
                if (!bucket.HasMetadata)
                {
                    _logger.LogDebug("Skipping emission for tx {TxId} until metadata arrives", entry.Key);
                    continue;
                }

                var resolution = completed
                    ? BucketResolution.Completed
                    : expired
                        ? BucketResolution.TtlExpired
                        : BucketResolution.LimitExceeded;
                emissions.Add((entry.Key, bucket, resolution));
                continue;
            }

            if (metadataChanged)
                updates.Add(new KeyValuePair<string, string>(entry.Key, TxBucketSerializer.Serialize(bucket)));
        }

        foreach (var update in updates) _stateStore.Put(update.Key, update.Value);

        foreach (var emission in emissions)
        {
            var payload = EmitBucket(emission.Key, emission.Bucket, emission.Resolution);
            _context.Forward(emission.Key, payload);
        }
    }

    private TxBucket LoadBucket(string txId, long createdAt)
    {
        if (_readOnlyStateStore == null) throw new InvalidOperationException("State store not initialized");

        var existing = _readOnlyStateStore.Get(txId);
        if (string.IsNullOrWhiteSpace(existing))
        {
            _metrics.BucketCreated();
            return TxBucket.Create(txId, createdAt);
        }

        try
        {
            return TxBucketSerializer.Deserialize(existing);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize bucket {TxId}. Recreating state entry.", txId);
            _stateStore!.Delete(txId);
            _metrics.BucketCreated();
            return TxBucket.Create(txId, createdAt);
        }
    }

    private void PersistBucket(string txId, TxBucket bucket)
    {
        if (_stateStore == null) throw new InvalidOperationException("State store not initialized");

        var serialized = TxBucketSerializer.Serialize(bucket);
        _stateStore.Put(txId, serialized);
    }

    private string EmitBucket(string txId, TxBucket bucket, BucketResolution resolution)
    {
        if (_stateStore == null) throw new InvalidOperationException("State store not initialized");

        var incomplete = resolution != BucketResolution.Completed || bucket.ExceededMaxEvents;
        var payload = TxAggregateBuilder.BuildAggregate(bucket, incomplete);
        _stateStore.Delete(txId);

        _metrics.AggregateEmitted();
        _metrics.RemoveBufferedEvents(bucket.ReceivedTotal);

        switch (resolution)
        {
            case BucketResolution.Completed:
                _metrics.BucketCompleted();
                _logger.LogInformation("Completed tx {TxId} ({Count} events)", txId, bucket.ReceivedTotal);
                break;
            case BucketResolution.TtlExpired:
                _metrics.BucketExpired();
                _logger.LogWarning(
                    "TTL expired for tx {TxId} after {Millis} ms with {Count} events",
                    txId,
                    _options.HardTtlMs,
                    bucket.ReceivedTotal);
                break;
            case BucketResolution.LimitExceeded:
                _metrics.BucketClosedByLimit();
                _logger.LogWarning(
                    "Closing tx {TxId} because max events ({Limit}) exceeded",
                    txId,
                    _options.MaxEventsPerBucket);
                break;
        }

        return payload;
    }

    private TxMetadata? FetchMetadata(string txId)
    {
        if (_metadataStore == null) return null;

        var raw = _metadataStore.Get(txId);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            return JsonSerializer.Deserialize<TxMetadata>(raw, MetadataSerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize metadata for tx {TxId}", txId);
            return null;
        }
    }

    private enum BucketResolution
    {
        Completed,
        TtlExpired,
        LimitExceeded
    }
}

internal static class TxAggregatorTransformerDependencies
{
    private static TxAggregatorOptions? _options;
    private static ILoggerFactory? _loggerFactory;
    private static TxAggregatorMetrics? _metrics;

    public static void Configure(
        TxAggregatorOptions options,
        ILoggerFactory loggerFactory,
        TxAggregatorMetrics metrics)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _metrics = metrics;
    }

    public static (TxAggregatorOptions Options, ILoggerFactory LoggerFactory, TxAggregatorMetrics Metrics) Resolve()
    {
        if (_options == null || _loggerFactory == null || _metrics == null)
            throw new InvalidOperationException("TxAggregatorTransformer dependencies are not configured.");

        return (_options, _loggerFactory, _metrics);
    }
}