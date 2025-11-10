using ChangeLogMonitor.DataAggregator.Processing;
using Xunit;

namespace ChangeLogMonitor.DataAggregator.Tests;

public class TxCdcParserTests
{
    [Fact]
    public void ExtractTxId_PrefersTransactionId()
    {
        const string json = "{\"payload\":{\"transaction\":{\"id\":\"tx-tr\"},\"after\":{\"tx_id\":\"fallback\"}}}";

        var txId = TxCdcParser.ExtractTxId(json);

        Assert.Equal("tx-tr", txId);
    }

    [Fact]
    public void ExtractTxId_FallsBackToAfter()
    {
        const string json = "{\"payload\":{\"after\":{\"tx_id\":\"tx-after\"}}}";

        var txId = TxCdcParser.ExtractTxId(json);

        Assert.Equal("tx-after", txId);
    }

    [Fact]
    public void TryParse_ReadsSourceAndPayload()
    {
        const string json = """
        {
          "payload": {
            "op": "c",
            "ts_ms": 1717698123456,
            "after": { "id": 1001, "customer_id": 456, "tx_id": "tx-1" },
            "source": { "table": "orders" },
            "transaction": { "id": "tx-1", "total_order": 3 }
          }
        }
        """;

        var parsed = TxCdcParser.TryParse(json, "tx-1", "topic:0:10", out var bucketEvent);

        Assert.True(parsed);
        Assert.Equal("orders", bucketEvent.Table);
        Assert.Equal("c", bucketEvent.Operation);
        Assert.Equal(1717698123456L, bucketEvent.TimestampMs);
        Assert.Equal(3L, bucketEvent.TotalOrder);
        Assert.Contains("customer_id", bucketEvent.PayloadJson);
    }
}
