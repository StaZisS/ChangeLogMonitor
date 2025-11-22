using System.Collections.Generic;
using System.Text.Json;
using ChangeLogMonitor.DataAggregator.Models;
using ChangeLogMonitor.DataAggregator.Processing;
using Xunit;

namespace ChangeLogMonitor.DataAggregator.Tests;

public class TxAggregateBuilderTests
{
    [Fact]
    public void BuildAggregate_MinimalExample_CompletesInTotalOrder()
    {
        var bucket = TxBucket.Create("tx-123", 1717698123000);
        Assert.Equal(TxBucketAddResult.Added,
            bucket.AddEvent(new TxBucketEvent("orders", "c", 1717698123456, 2, "{\"id\":1001}", "topic:0:1"), 10));
        Assert.Equal(TxBucketAddResult.Added,
            bucket.AddEvent(new TxBucketEvent("order_items", "c", 1717698123499, 3, "{\"sku\":\"ABC\"}", "topic:0:2"),
                10));
        Assert.Equal(TxBucketAddResult.Added,
            bucket.AddEvent(new TxBucketEvent("order_items", "c", 1717698123470, 1, "{\"sku\":\"XYZ\"}", "topic:0:3"),
                10));

        using var metaDoc = JsonDocument.Parse("{\"user\":\"alice\",\"reason\":\"import\"}");
        bucket.ApplyMetadata(new TxMetadata
        {
            TxId = "tx-123",
            ExpectedTotal = 3,
            ExpectedByTable = new Dictionary<string, int>
            {
                ["orders"] = 1,
                ["order_items"] = 2
            },
            Ordering = "total_order",
            Meta = metaDoc.RootElement.Clone()
        });

        var json = TxAggregateBuilder.BuildAggregate(bucket, false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.False(root.GetProperty("incomplete").GetBoolean());
        Assert.Equal("tx-123", root.GetProperty("tx_id").GetString());
        Assert.Equal("alice", root.GetProperty("meta").GetProperty("user").GetString());

        var events = root.GetProperty("events");
        Assert.Equal(3, events.GetArrayLength());
        Assert.Equal(1, events[0].GetProperty("total_order").GetInt64());
        Assert.Equal(2, events[1].GetProperty("total_order").GetInt64());
        Assert.Equal(3, events[2].GetProperty("total_order").GetInt64());

        var stats = root.GetProperty("stats");
        Assert.Equal(3, stats.GetProperty("received_total").GetInt32());
        Assert.Equal(3, stats.GetProperty("expected_total").GetInt32());
    }
}