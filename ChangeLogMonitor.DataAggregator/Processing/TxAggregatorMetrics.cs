using System;
using System.Threading;

namespace ChangeLogMonitor.DataAggregator.Processing;

public sealed class TxAggregatorMetrics
{
    private long _bucketsCreated;
    private long _activeBuckets;
    private long _bucketsCompleted;
    private long _bucketsExpired;
    private long _bucketsPartial;
    private long _bucketsLimitExceeded;
    private long _duplicateEvents;
    private long _missingTxId;
    private long _parseFailures;
    private long _cdcEventsProcessed;
    private long _eventsBuffered;
    private long _aggregatesEmitted;

    public void BucketCreated()
    {
        Interlocked.Increment(ref _bucketsCreated);
        Interlocked.Increment(ref _activeBuckets);
    }

    public void BucketCompleted()
    {
        Interlocked.Increment(ref _bucketsCompleted);
        DecrementActiveBuckets();
    }

    public void BucketExpired()
    {
        Interlocked.Increment(ref _bucketsExpired);
        Interlocked.Increment(ref _bucketsPartial);
        DecrementActiveBuckets();
    }

    public void BucketClosedByLimit()
    {
        Interlocked.Increment(ref _bucketsLimitExceeded);
        Interlocked.Increment(ref _bucketsPartial);
        DecrementActiveBuckets();
    }

    public void MarkDuplicateEvent() => Interlocked.Increment(ref _duplicateEvents);

    public void MarkMissingTxId() => Interlocked.Increment(ref _missingTxId);

    public void MarkParseFailure() => Interlocked.Increment(ref _parseFailures);

    public void MarkCdcEventProcessed() => Interlocked.Increment(ref _cdcEventsProcessed);

    public void AddBufferedEvent() => Interlocked.Increment(ref _eventsBuffered);

    public void RemoveBufferedEvents(int count)
    {
        if (count <= 0) return;
        Interlocked.Add(ref _eventsBuffered, -count);
        if (Interlocked.Read(ref _eventsBuffered) < 0)
        {
            Interlocked.Exchange(ref _eventsBuffered, 0);
        }
    }

    public void AggregateEmitted() => Interlocked.Increment(ref _aggregatesEmitted);

    public TxAggregatorMetricsSnapshot GetSnapshot() => new(
        Interlocked.Read(ref _bucketsCreated),
        Math.Max(0, Interlocked.Read(ref _activeBuckets)),
        Interlocked.Read(ref _bucketsCompleted),
        Interlocked.Read(ref _bucketsExpired),
        Interlocked.Read(ref _bucketsPartial),
        Interlocked.Read(ref _bucketsLimitExceeded),
        Interlocked.Read(ref _duplicateEvents),
        Interlocked.Read(ref _missingTxId),
        Interlocked.Read(ref _parseFailures),
        Interlocked.Read(ref _cdcEventsProcessed),
        Math.Max(0, Interlocked.Read(ref _eventsBuffered)),
        Interlocked.Read(ref _aggregatesEmitted));

    private void DecrementActiveBuckets()
    {
        var value = Interlocked.Decrement(ref _activeBuckets);
        if (value < 0)
        {
            Interlocked.Exchange(ref _activeBuckets, 0);
        }
    }
}

public sealed record TxAggregatorMetricsSnapshot(
    long BucketsCreated,
    long ActiveBuckets,
    long BucketsCompleted,
    long BucketsExpired,
    long BucketsPartial,
    long BucketsLimitExceeded,
    long DuplicateEvents,
    long MissingTxIdEvents,
    long ParseFailures,
    long CdcEventsProcessed,
    long BufferedEvents,
    long AggregatesEmitted);
