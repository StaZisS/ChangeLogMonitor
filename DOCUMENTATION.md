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

## Архитектура

### Поток данных

```
┌─────────────────┐     ┌───────────┐     ┌─────────────────┐
│   Application   │────▶│  Kafka    │────▶│  DataAggregator │
│   + Interceptor │     │           │     │                 │
└─────────────────┘     └───────────┘     └────────┬────────┘
        │                     ▲                    │
        │                     │                    ▼
        ▼                     │           ┌─────────────────┐
┌─────────────────┐     ┌─────┴─────┐     │  Finalization   │
│   audit_log     │────▶│  Debezium │     │  (ClickHouse)   │
│   (PostgreSQL)  │     │           │     └────────┬────────┘
└─────────────────┘     └───────────┘              │
                                                   ▼
                                          ┌─────────────────┐
                                          │       UI        │
                                          │  (Razor Pages)  │
                                          └─────────────────┘
```

### Граф зависимостей модулей

```
Core (без зависимостей)
  ├── Configuration → Core
  ├── Interceptor → Core, Configuration
  ├── DataAggregator → Core
  ├── Finalization → Core, Configuration, DataAggregator
  ├── UI → Core, Finalization
  ├── Embedded → Core, Interceptor, Configuration
  └── Standalone → UI, Finalization, DataAggregator
```

---

## Модули

### ChangeLogMonitor.Core

Базовый модуль с интерфейсами, моделями и перечислениями. Не имеет зависимостей.

**Структура:**
- `Models/` - модели данных (AuditLogEntry, FieldChange, CollectionChange, Policy/*)
- `Interfaces/` - интерфейсы (IAccessControlService, ICurrentUserService, IEnumLabelProvider)
- `Enums/` - перечисления (AuditMode, AuditOperationType, FieldAction, FieldType и др.)
- `Attributes/` - атрибуты (AuditEnumLabelAttribute)

---

### ChangeLogMonitor.Configuration

Загрузка и валидация YAML политики аудита.

**Возможности:**
- Whitelist/blacklist режимы
- Маскирование, хеширование, шифрование полей
- Денормализация ссылок (FK)
- Отслеживание дельт коллекций
- Форматирование view-значений
- Система пресетов

**Использование:**

```csharp
// Регистрация
builder.Services.AddAuditConfiguration();
// или с указанием пути
builder.Services.AddAuditConfiguration("path/to/config.yaml");

// Использование
public class MyService
{
    private readonly IAuditConfigurationService _configService;

    public void Example()
    {
        AuditPolicy policy = _configService.GetPolicy();
        EntityPolicy? userPolicy = _configService.GetEntityPolicy("User");
        bool isEnabled = _configService.IsEntityEnabled("User");
        _configService.ReloadConfiguration();
    }
}
```

**Минимальная конфигурация:**

```yaml
auditPolicy:
  version: "1.0"
  mode: whitelist

  entities:
    User:
      enabled: true
      fields:
        Password: exclude
        Email: include
```

---

### ChangeLogMonitor.Interceptor

EF Core интерцептор для захвата изменений при `SaveChanges`.

**Функции:**
- Перехват SaveChanges/SaveChangesAsync
- Применение YAML-политики
- Сериализация метаданных в protobuf
- Запись в `audit_log` в той же транзакции
- Автоматические миграции (опционально)
- Кастомный `IAuditMetadataProvider`

**Быстрый старт:**

```csharp
// 1. Регистрация интерцептора
builder.Services.AddChangeLogInterceptor(
    auditDbConnectionString: builder.Configuration.GetConnectionString("AuditDb")!,
    configFilePath: "changelog-config.yaml",
    metadataProviderFactory: sp => new HttpContextAuditMetadataProvider(
        sp.GetRequiredService<IHttpContextAccessor>()),
    applyMigrations: true);

// 2. Подключение к DbContext
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddChangeLogInterceptor(sp);
});
```

**Таблица audit_log:**

| Колонка | Тип | Описание |
|---------|-----|----------|
| id | bigint | Identity PK |
| transaction_id | varchar(255) | ID транзакции |
| payload | bytea | Сериализованный AuditMetaEnvelope |
| created_at | timestamptz | Время фиксации (UTC) |
| processed_at | timestamptz? | Время обработки |

**Кастомный MetadataProvider:**

```csharp
public sealed class HttpContextAuditMetadataProvider : IAuditMetadataProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextAuditMetadataProvider(IHttpContextAccessor accessor) =>
        _httpContextAccessor = accessor;

    public string GetUserId() =>
        _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value ?? "anonymous";

    public string GetUserName() =>
        _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "anonymous";

    public string? GetRequestId() => _httpContextAccessor.HttpContext?.TraceIdentifier;
    public string? GetServiceName() => "MyApp.Api";
    public string? GetClientIp() =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? GetUserAgent() =>
        _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
    public Dictionary<string, string>? GetHints() => null;
}
```

**Тесты:**
```bash
dotnet test ChangeLogMonitor.Interceptor.Tests
```
Требуется Docker для Testcontainers (PostgreSQL 16-alpine).

---

### ChangeLogMonitor.DataAggregator

Kafka stream-процессор для агрегации CDC событий.

**Возможности:**
- Exactly-once семантика с RocksDB state store
- Группировка событий по `tx_id`
- GlobalKTable для метаданных
- Дедупликация по topic/partition/offset
- TTL для неполных транзакций
- Health endpoint с метриками

**Конфигурация (appsettings.json):**

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
      "DlqTopic": "aggregator.dlq"
    },
    "Processing": {
      "FlushIntervalMs": 800,
      "HardTtlMs": 2000,
      "MaxEventsPerBucket": 1000
    },
    "Http": {
      "Port": 8080
    }
  }
}
```

**Запуск:**
```bash
dotnet run --project ChangeLogMonitor.DataAggregator
```

**Health endpoint:** `GET /healthz`
```json
{
  "status": "RUNNING",
  "running": true,
  "ready": true,
  "metrics": {
    "bucketsCreated": 12,
    "activeBuckets": 2,
    "bucketsCompleted": 10,
    "duplicateEvents": 3,
    "cdcEventsProcessed": 123
  }
}
```

---

### ChangeLogMonitor.Finalization

ClickHouse sink и REST API для дифов.

**Возможности:**
- Консьюмер Kafka (топик агрегатов)
- Хранение в ClickHouse (MergeTree, месячные партиции)
- REST API для выборки дифов
- Автосоздание таблицы

**Конфигурация:**

```json
{
  "App": {
    "Kafka": {
      "BootstrapServers": "localhost:9092",
      "InputTopic": "aggregates.by_tx",
      "GroupId": "changemonitor-finalizer"
    },
    "ClickHouse": {
      "ConnectionString": "Host=localhost;Port=9000;Database=default",
      "TableName": "audit_log",
      "EnsureSchema": true
    },
    "Http": {
      "Port": 8081
    }
  }
}
```

**API:**
- `GET /diffs/{table}/{entityId}?limit=50` - дифы по сущности
- `GET /diffs/tx/{txId}?limit=50` - дифы по транзакции
- `GET /healthz` - liveness probe

**ClickHouse DDL:**
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
ORDER BY (table_name, user_id, change_time);
```

---

### ChangeLogMonitor.UI

Razor Pages UI для журнала изменений.

**Функции:**
- Просмотр журнала изменений
- Фильтрация и поиск
- Детали изменений (до/после)
- Экспорт данных

---

### ChangeLogMonitor.Embedded

Библиотека для встраивания аудита в приложение.

**Назначение:**
- Регистрация интерцептора в EF Core
- Регистрация конфигурации
- Настройка всех сервисов

Используется когда нужно встроить аудит в основное приложение.

---

### ChangeLogMonitor.Standalone

Standalone приложение для развертывания как отдельного сервиса.

**Включает:**
- UI
- Finalization (ClickHouse sink + API)
- DataAggregator

**Не включает:**
- Interceptor (работает только с Debezium)

---

### ChangeLogMonitor.TestHarness

Тестовый стенд для разработки.

**Возможности:**
- SQLite по умолчанию, PostgreSQL через connection string
- Демо-пользователь: `demo-user` / `demo-pass`
- JWT аутентификация: `POST /auth/token`
- Swagger UI: `/swagger`
- Docker Compose с Kafka, Debezium, Postgres

**Запуск:**
```bash
dotnet run --project ChangeLogMonitor.TestHarness
```

**Docker стенд:**
```bash
cd ChangeLogMonitor.TestHarness
docker compose up -d
```

Запустятся: Postgres, Kafka (KRaft), Debezium Connect, Kafka UI (`http://localhost:8080`).

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
