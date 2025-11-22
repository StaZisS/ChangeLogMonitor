using ChangeLogMonitor.DataAggregator.Processing;
using Xunit;

namespace ChangeLogMonitor.DataAggregator.Tests;

public class TxCdcParserTests
{
    [Fact]
    public void ExtractTxId_PrefersDebeziumTransactionId()
    {
        const string json =
            "{\"payload\":{\"transaction\":{\"id\":\"debez\"},\"after\":{\"transaction_id\":\"biz-id\"}}}";

        var txId = TxCdcParser.ExtractTxId(json);

        Assert.Equal("debez", txId);
    }

    [Fact]
    public void ExtractTxId_ExtractsOnlyXidFromPostgresFormat()
    {
        // Debezium PostgreSQL формирует transaction.id как "xid:lsn"
        const string json = "{\"payload\":{\"transaction\":{\"id\":\"763:27659832\"},\"after\":{\"id\":1}}}";

        var txId = TxCdcParser.ExtractTxId(json);

        Assert.Equal("763", txId);
    }

    [Fact]
    public void ExtractTxId_GroupsSameTransactionByXid()
    {
        // Две записи из одной транзакции PostgreSQL должны группироваться по одному xid
        const string json1 = "{\"payload\":{\"transaction\":{\"id\":\"763:27659832\"}}}";
        const string json2 = "{\"payload\":{\"transaction\":{\"id\":\"763:27660776\"}}}";

        var txId1 = TxCdcParser.ExtractTxId(json1);
        var txId2 = TxCdcParser.ExtractTxId(json2);

        Assert.Equal("763", txId1);
        Assert.Equal("763", txId2);
        Assert.Equal(txId1, txId2);
    }

    [Fact]
    public void ExtractTxId_FallsBackToBusinessTransactionId()
    {
        const string json = "{\"payload\":{\"after\":{\"transaction_id\":\"tx-after\"}}}";

        var txId = TxCdcParser.ExtractTxId(json);

        Assert.Equal("tx-after", txId);
    }

    [Fact]
    public void ExtractTxId_WorksWithoutPayloadWrapper()
    {
        const string json = "{\"transaction\":{\"id\":\"tx-root\"}}";

        var txId = TxCdcParser.ExtractTxId(json);

        Assert.Equal("tx-root", txId);
    }

    [Fact]
    public void ExtractTxId_FallsBackToDebeziumTransaction()
    {
        const string json = "{\"payload\":{\"transaction\":{\"id\":\"debez\"}}}";

        var txId = TxCdcParser.ExtractTxId(json);

        Assert.Equal("debez", txId);
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