using System;
using System.Collections.Generic;
using ChangeLogMonitor.DataAggregator.Processing;
using ChangeLogMonitor.DataAggregator.Models;
using Xunit;

namespace ChangeLogMonitor.DataAggregator.Tests;

public class TxBucketTests
{
    [Fact]
    public void IsComplete_ByTotal()
    {
        var bucket = TxBucket.Create("tx-42", 1);
        bucket.ExpectedTotal = 2;

        var first = new TxBucketEvent("orders", "c", 10, 1, "{}", "topic:0:1");
        var second = new TxBucketEvent("orders", "u", 11, 2, "{}", "topic:0:2");

        Assert.Equal(TxBucketAddResult.Added, bucket.AddEvent(first, 10));
        Assert.Equal(TxBucketAddResult.Added, bucket.AddEvent(second, 10));

        Assert.True(bucket.IsComplete());
    }

    [Fact]
    public void IsComplete_ByTableCounts()
    {
        var bucket = TxBucket.Create("tx-42", 1);
        bucket.ExpectedByTable = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["orders"] = 1,
            ["order_items"] = 2
        };

        Assert.Equal(TxBucketAddResult.Added, bucket.AddEvent(new TxBucketEvent("orders", "c", 10, 1, "{}", "t:0:1"), 10));
        Assert.Equal(TxBucketAddResult.Added, bucket.AddEvent(new TxBucketEvent("order_items", "c", 11, 2, "{}", "t:0:2"), 10));
        Assert.Equal(TxBucketAddResult.Added, bucket.AddEvent(new TxBucketEvent("order_items", "c", 12, 3, "{}", "t:0:3"), 10));

        Assert.True(bucket.IsComplete());
    }

    [Fact]
    public void ApplyMetadata_SetsFlag()
    {
        var bucket = TxBucket.Create("tx-1", 1);
        bucket.ApplyMetadata(new TxMetadata
        {
            ExpectedTotal = 1
        });

        Assert.True(bucket.HasMetadata);
        Assert.Equal(1, bucket.ExpectedTotal);
    }

    [Fact]
    public void Serializer_PreservesHasMetadata()
    {
        var bucket = TxBucket.Create("tx-1", 1);
        bucket.HasMetadata = true;

        var serialized = TxBucketSerializer.Serialize(bucket);
        var restored = TxBucketSerializer.Deserialize(serialized);

        Assert.True(restored.HasMetadata);
    }
}
