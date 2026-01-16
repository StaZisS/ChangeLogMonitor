# ChangeLogMonitor.DataAggregator

Kafka stream-процессор для агрегации CDC событий.

**Возможности:**
- Exactly-once семантика с RocksDB
- Группировка событий по `tx_id`
- GlobalKTable для метаданных
- Дедупликация

**Запуск:**
```bash
dotnet run --project ChangeLogMonitor.DataAggregator
```

Подробная документация: [DOCUMENTATION.md](../DOCUMENTATION.md#changelogmonitordataaggregator)
