using System.Text.Json;
using Audit.V1;
using ChangeLogMonitor.Finalization.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLogMonitor.Finalization.Tests;

public class AggregateFlattenerTests
{
    [Fact]
    public void Flatten_ProducesAuditRecordPayload()
    {
        // arrange: minimal aggregate with one update event
        var aggregateJson = """
        {
          "tx_id": "tx-123",
          "meta": { "user_id": "u-1" },
          "events": [
            {
              "table": "orders",
              "op": "u",
              "ts_ms": 1717698123456,
              "payload": { "id": "order-1", "amount": 10 }
            }
          ]
        }
        """;

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>());

        // act
        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);

        // assert
        var record = Assert.Single(rows);
        Assert.Equal("orders", record.TableName);
        Assert.Equal("order-1", record.EntityId);
        Assert.Equal((byte)2, record.OperationCode); // 'u' -> Update

        var parsed = AuditRecord.Parser.ParseFrom(Convert.FromBase64String(record.Payload));
        Assert.Equal("tx-123-0", parsed.Id);
        Assert.Equal("orders", parsed.EntityType);
        Assert.Equal("order-1", parsed.EntityId);
        Assert.Equal(OperationType.OperationUpdate, parsed.Operation);
        Assert.Equal("u-1", parsed.UserId);

        var payloadDoc = JsonDocument.Parse(parsed.RawPayloadJson);
        Assert.Equal("order-1", payloadDoc.RootElement.GetProperty("id").GetString());
    }
}
