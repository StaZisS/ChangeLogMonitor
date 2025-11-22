# ChangeLogMonitor.DataAggregator

A production-ready .NET 9 background service that aggregates Debezium CDC events into a single Kafka message per
transaction. It consumes CDC rows from multiple topics, enriches them via metadata records, buffers intermediate state
in RocksDB, and emits a sorted aggregate when the transaction is complete or the TTL expires.

## Features

- **Exactly-once** Streamiz topology with RocksDB state store and persistent repartition topic.
- **Global metadata** lookup per transaction (expected totals, user-defined meta, ordering hints).
- **Transformer-based aggregation** with deduplication, TTL sweeps, and optional early emission when
  `MaxEventsPerBucket` is reached.
- **Structured logging + counters** surfaced through `/healthz`, including bucket lifecycle metrics.
- **Self-contained Docker image** and configurable `appsettings.json` / environment overrides.

## Configuration

Configuration lives under the `App` section (see [`appsettings.json`](./appsettings.json)). Environment variables can
override any value using `App__Section__Property` syntax.

```json
{
  "App": {
    "Kafka": {
      "BootstrapServers": "localhost:9092",
      "ApplicationId": "changemonitor-data-aggregator",
      "InputCdcTopics": ["changelog.all"],
      "MetadataTopic": "app.transaction_meta",
      "OutputTopic": "aggregates.by_tx",
      "RepartitionTopic": "agg.changes.by_tx",
      "DlqTopic": "aggregator.dlq",
      "EnableAutoTopicCreation": false
    },
    "Processing": {
      "FlushIntervalMs": 800,
      "HardTtlMs": 2000,
      "MaxEventsPerBucket": 1000,
      "RejectWithoutTxId": true,
      "EmitPartialOnLimit": true
    },
    "Http": {
      "Port": 8080
    }
  }
}
```

> **Topics required**
>
> - Debezium CDC topics (array) – e.g. `db.inventory.orders`, `db.inventory.order_items`
> - Metadata topic – `app.transaction_meta` (key = `tx_id`)
> - Repartition topic – `agg.changes.by_tx`
> - Output topic – `aggregates.by_tx`
> - (Optional) DLQ for malformed CDC – `aggregator.dlq`

## Running locally

```bash
# Ensure Kafka is reachable and topics exist
export App__Kafka__BootstrapServers=localhost:9092
export App__Kafka__InputCdcTopics__0=changelog.all

# Run the service
cd ChangeLogMonitor.DataAggregator
dotnet run
```

The HTTP health endpoint listens on `http://0.0.0.0:8080/healthz` by default and reports readiness plus metric counters.

### Docker

```bash
cd ChangeLogMonitor.DataAggregator
docker build -t changelog-aggregator .

docker run --rm \
  -p 8080:8080 \
  -e App__Kafka__BootstrapServers=broker:9092 \
  -e App__Kafka__InputCdcTopics__0=changelog.all \
  changelog-aggregator
```

The image is self-contained (`linux-x64`, single-file publish) and exposes port `8080`.

## Message flow

1. CDC records are re-keyed by `tx_id`, validated, and sent through the repartition topic `agg.changes.by_tx` so that
   all records for a transaction hit the same task.
2. A `GlobalKTable` materializes metadata (`expected_total`, `expected_by_table`, `ordering`, `meta`).
3. `TxAggregatorTransformer` buffers CDC rows per transaction bucket in a RocksDB store, deduplicates by
   topic/partition/offset, and checks completeness on every event and via punctuation (every 800 ms):
    - `received_total == expected_total`, or
    - All per-table counts match `expected_by_table`.
4. On completion, the transformer emits a sorted aggregate to `aggregates.by_tx`. If TTL (2 s default) expires first, it
   emits `incomplete=true` and deletes the bucket.
5. Metrics such as buckets created/completed, TTL expirations, max-event closures, duplicates, and missing `tx_id`
   counts are exposed via `/healthz`.

### Sample CDC payload

```json
{
  "payload": {
    "op": "c",
    "ts_ms": 1717698123456,
    "after": {
      "id": 1001,
      "customer_id": 456,
      "amount": 99.9,
      "tx_id": "tx-123"
    },
    "source": { "table": "orders" },
    "transaction": { "id": "tx-123", "total_order": 1 }
  }
}
```

### Metadata record

```json
{
  "tx_id": "tx-123",
  "expected_total": 3,
  "expected_by_table": { "orders": 1, "order_items": 2 },
  "ordering": "total_order",
  "meta": { "user": "alice", "reason": "import" }
}
```

### Output aggregate

```json
{
  "tx_id": "tx-123",
  "meta": { "user": "alice", "reason": "import" },
  "incomplete": false,
  "events": [
    {
      "table": "orders",
      "op": "c",
      "ts_ms": 1717698123456,
      "total_order": 1,
      "payload": { "id": 1001, "customer_id": 456, "amount": 99.9 }
    },
    {
      "table": "order_items",
      "op": "c",
      "ts_ms": 1717698123499,
      "total_order": 2,
      "payload": { "order_id": 1001, "sku": "ABC", "qty": 2 }
    }
  ],
  "stats": {
    "expected_total": 3,
    "received_total": 3,
    "expected_by_table": { "orders": 1, "order_items": 2 },
    "received_by_table": { "orders": 1, "order_items": 2 }
  }
}
```

## Health & metrics

`GET /healthz` returns:

```json
{
  "status": "RUNNING",
  "running": true,
  "ready": true,
  "metrics": {
    "bucketsCreated": 12,
    "activeBuckets": 2,
    "bucketsCompleted": 10,
    "bucketsExpired": 1,
    "bucketsPartial": 1,
    "bucketsLimitExceeded": 0,
    "duplicateEvents": 3,
    "missingTxIdEvents": 0,
    "parseFailures": 0,
    "cdcEventsProcessed": 123,
    "bufferedEvents": 4,
    "aggregatesEmitted": 11
  }
}
```

Use it for liveness/readiness probes and to monitor aggregation activity.

## Testing

Unit tests cover `TxCdcParser`, completeness logic, and aggregate sorting rules.

```bash
dotnet test ChangeLogMonitor.DataAggregator.Tests/ChangeLogMonitor.DataAggregator.Tests.csproj
```

Ensure Kafka topics exist and Debezium emits sample data when running the service end-to-end.
