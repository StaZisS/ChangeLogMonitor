# ChangeLogMonitor - Документация

Модульная система аудита изменений данных на основе Debezium CDC.

## Содержание

1. [Обзор проекта](#обзор-проекта)
2. [Архитектура](#архитектура)
3. [Модули](#модули)
4. [Режимы развертывания](#режимы-развертывания)
5. [Быстрый старт](#быстрый-старт)
6. [Требования](#требования)
7. [Команды сборки и запуска](#команды-сборки-и-запуска)

---

## Обзор проекта

ChangeLogMonitor отслеживает и визуализирует все изменения данных в базе данных. Система построена на Change Data Capture (CDC) с использованием Debezium и работает в двух режимах: встроенно в приложение или как отдельный сервис.

### Принцип работы

1. **Захват изменений**: EF Core интерцептор записывает изменения в таблицу `audit_log` с метаданными (кто, когда, контекст)
2. **CDC через Debezium**: Debezium отслеживает изменения и отправляет события в Kafka
3. **Агрегация**: DataAggregator получает CDC события, соединяет данные с метаданными
4. **Финализация**: Данные сохраняются в ClickHouse и доступны через REST API
5. **Визуализация**: UI показывает журнал изменений с фильтрацией и поиском

---

## Режимы развертывания

### Embedded (встроенный)

Используйте NuGet пакет `ChangeLogMonitor.Embedded` в вашем приложении.

**Плюсы:**
- Простота интеграции
- Захват изменений в процессе приложения
- Единая транзакция

**Минусы:**
- Дополнительная нагрузка на приложение

### Standalone (отдельный сервис)

Разверните `ChangeLogMonitor.Standalone` как отдельный сервис.

**Плюсы:**
- Изоляция от основного приложения
- Независимое масштабирование

**Минусы:**
- Требует настройки Debezium/Kafka
- Отложенная обработка изменений

---

## Быстрый старт

1. **Скопируйте конфигурацию:**
   ```bash
   cp changelog-config.example.yaml changelog-config.yaml
   cp appsettings.example.json appsettings.json
   ```

2. **Настройте подключения в appsettings.json**

3. **Запустите:**
   ```bash
   # Standalone
   dotnet run --project ChangeLogMonitor.Standalone

   # Или TestHarness для разработки
   dotnet run --project ChangeLogMonitor.TestHarness
   ```

---

## Требования

- .NET 9.0
- Entity Framework Core 9
- Kafka
- Debezium
- ClickHouse (для Finalization)
- PostgreSQL/SQL Server/MySQL

---

## Команды сборки и запуска

```bash
# Сборка
dotnet build ChangeLogMonitor.sln
dotnet build ChangeLogMonitor.sln -c Release

# Запуск
dotnet run --project ChangeLogMonitor.Standalone
dotnet run --project ChangeLogMonitor.TestHarness

# Тесты
dotnet test ChangeLogMonitor.Interceptor.Tests
dotnet test ChangeLogMonitor.DataAggregator.Tests
dotnet test ChangeLogMonitor.Configuration.Tests

# Миграции (Interceptor)
dotnet ef database update \
  --project ChangeLogMonitor.Interceptor \
  --context ChangeLogMonitor.Interceptor.Models.AuditDbContext
```

---

## Помощь в конфигурации

Для помощи в синтаксисе созданы подсказки в rider

Для активации необходимо

```
 1. Settings → Languages & Frameworks → Schemas and DTDs → JSON Schema Mappings                                                                                                                                                                                                                                 
 2. Нажать +                                                                                                                                                                                                                                                                                                    
 3. Заполнить:                                                                                                                                                                                                                                                                                                  
   - Name: ChangeLogMonitor Config                                                                                                                                                                                                                                                                              
   - Schema file or URL: выбрать changelog-config.schema.yaml                                                                                                                                                                                                                                                   
   - Нажать + в нижней части и добавить File path pattern: changelog-config*.yaml                                                                                                                                                                                                                               
 4. Apply → OK 
```

## UI

localhost:5000/AuditLog
