# ChangeLogMonitor

Модульная система аудита изменений данных на основе Debezium CDC.

## Возможности

- Отслеживание всех изменений в базе данных (Create/Update/Delete)
- Маскирование, хеширование, шифрование чувствительных данных
- Денормализация ссылок (FK) и отслеживание дельт коллекций
- Гибкая YAML-конфигурация политики аудита
- Два режима развертывания: встроенный и standalone

## Архитектура

```
Application → Interceptor → audit_log → Debezium → Kafka → DataAggregator → ClickHouse → UI
```

## Быстрый старт

```bash
# 1. Скопировать конфигурацию
cp changelog-config.example.yaml changelog-config.yaml
cp appsettings.example.json appsettings.json

# 2. Запустить
dotnet run --project ChangeLogMonitor.Standalone
```

## Модули

| Модуль | Описание |
|--------|----------|
| Core | Базовые модели и интерфейсы |
| Configuration | YAML политика аудита |
| Interceptor | EF Core интерцептор |
| DataAggregator | Kafka агрегация |
| Finalization | ClickHouse sink + API |
| UI | Razor Pages интерфейс |
| Embedded | Библиотека для встраивания |
| Standalone | Отдельный сервис |

## Документация

- **[DOCUMENTATION.md](DOCUMENTATION.md)** - полная документация
- **[CONFIGURATION.md](CONFIGURATION.md)** - справочник конфигурации
- **[CLAUDE.md](CLAUDE.md)** - инструкции для Claude Code

## Требования

- .NET 9.0
- Kafka + Debezium
- ClickHouse
- PostgreSQL/SQL Server/MySQL

## Лицензия

MIT
