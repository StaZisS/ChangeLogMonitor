# ChangeLogMonitor.Finalization

Финализатор, который забирает агрегированные события из Kafka, кладёт их в ClickHouse и отдаёт дифы по HTTP.

## Возможности

- Консьюмер Kafka (топик агрегатов из `ChangeLogMonitor.DataAggregator`).
- Сохранение в ClickHouse (`MergeTree`, партиция по месяцу изменения) с `log_id` для seek-пагинации.
- REST API для выборки дифов по сущности или транзакции.
- Автосоздание таблицы (можно отключить в настройках).
- Пэйлоад хранится как base64 protobuf `audit.v1.AuditRecord` (см. `ChangeLogMonitor.Protos/Protos/audit_record.proto`).

## Конфигурация

`appsettings.json` (или переменные окружения через префикс `App__`) содержит:

```json
{
  "App": {
    "Kafka": {
      "BootstrapServers": "localhost:9092",
      "InputTopic": "aggregates.by_tx",
      "GroupId": "changemonitor-finalizer",
      "EnableAutoCreateTopics": false
    },
    "ClickHouse": {
      "ConnectionString": "Host=localhost;Port=9000;Database=default;User=default;Password=",
      "TableName": "audit_log",
      "EnsureSchema": true
    },
    "Http": {
      "Port": 8081
    }
  }
}
```

## Таблица в ClickHouse

DDL по умолчанию (создаётся при старте, если `EnsureSchema=true`):

```sql
CREATE TABLE IF NOT EXISTS audit_log (
  change_time DateTime,
  user_id String,
  table_name LowCardinality(String),
  operation UInt8,
  entity_id String,
  tx_id String,
  payload String
) ENGINE = MergeTree()
PARTITION BY toYYYYMM(change_time)
ORDER BY (table_name, user_id, change_time)
SETTINGS index_granularity = 8192;
```

`operation`: 1=insert, 2=update, 3=delete, 0=unknown/snapshot.

## API

- `GET /diffs/{table}/{entityId}?limit=50` — дифы по сущности (DESC по времени).
- `GET /diffs/tx/{txId}?limit=50` — дифы по транзакции.
- `GET /healthz` — liveness + краткая сводка.

## Запуск

```bash
dotnet run --project ChangeLogMonitor.Finalization
```

Минимальные переменные для продакшена:

```
App__Kafka__BootstrapServers=broker:9092
App__Kafka__InputTopic=aggregates.by_tx
App__Kafka__GroupId=finalizer
App__ClickHouse__ConnectionString=Host=ch:9000;Database=audits;User=app;Password=secret
```
