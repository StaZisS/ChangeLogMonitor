# ChangeLogMonitor.Interceptor

EF Core-интерцептор, который фиксирует изменения данных и записывает метаданные в таблицу `audit_log`. Эти записи затем подбираются Debezium и используются остальными модулями ChangeLogMonitor для построения человекочитаемого журнала.

## Что делает модуль

- перехватывает `SaveChanges`/`SaveChangesAsync` всех `DbContext`, кроме собственного `AuditDbContext`;
- применяет YAML-политику (`changelog-config.yaml`) через `ChangeLogMonitor.Configuration`, чтобы исключить неаудируемые сущности/поля;
- сериализует метаданные в protobuf (`AuditMetaEnvelope` из `ChangeLogMonitor.Protos`);
- записывает payload и `transaction_id` в `audit_log`, используя ту же транзакцию, что и бизнес-операция;
- автоматически применяет EF Core миграции (опционально) и проверяет наличие таблицы перед использованием;
- позволяет подключить собственный `IAuditMetadataProvider`.

## Состав проекта

```
ChangeLogMonitor.Interceptor/
├── ChangeLogMonitor.Interceptor.csproj   # net9.0, EF Core 9, Npgsql
├── Extensions/                           # Методы для DI и DbContextOptionsBuilder
├── Interceptors/                         # ChangeLogInterceptor (SaveChangesInterceptor)
├── Models/                               # AuditDbContext и сущность AuditLog
├── Services/                             # Сериализация, миграции, metadata provider
├── Migrations/                           # EF Core миграции audit_log
└── README.md
```

## Требования

- .NET 9 SDK;
- PostgreSQL (используется Npgsql; для других БД потребуется адаптация `AuditDbContext`);
- файл `changelog-config.yaml` (можно скопировать из `changelog-config.example.yaml`);
- Debezium + Kafka (если нужно ретранслировать `audit_log` дальше, для локальной интеграции не обязательно).

## Быстрый старт

1. **Готовим конфигурацию**  
   ```bash
   cp changelog-config.example.yaml changelog-config.yaml
   ```

2. **Регистрируем интерцептор и инфраструктуру**  
   ```csharp
   using ChangeLogMonitor.Interceptor.Extensions;

   builder.Services.AddChangeLogInterceptor(
       auditDbConnectionString: builder.Configuration.GetConnectionString("AuditDb")!,
       configFilePath: builder.Environment.ContentRootPath + "/changelog-config.yaml",
       metadataProviderFactory: sp => new HttpContextAuditMetadataProvider(
           sp.GetRequiredService<IHttpContextAccessor>()),
       applyMigrations: true);
   ```

   - `metadataProviderFactory` необязателен; по умолчанию используется `DefaultAuditMetadataProvider`.
   - `applyMigrations` заключается в добавлении `AuditDbMigrationHostedService`, который вызывает `context.Database.Migrate()` при старте. Выключите его, если схема управляется вручную.

3. **Подключаем DbContext приложения**  
   ```csharp
   builder.Services.AddDbContext<AppDbContext>((sp, options) =>
   {
       options.UseNpgsql(builder.Configuration.GetConnectionString("AppDb"));
       options.AddChangeLogInterceptor(sp); // extension вытянет ChangeLogInterceptor из DI
   });
   ```

4. **(Опционально) ручной запуск миграции**  
   ```bash
   dotnet ef database update \
     --project ChangeLogMonitor.Interceptor \
     --context ChangeLogMonitor.Interceptor.Models.AuditDbContext
   ```

После этого каждое `SaveChanges()` будет записывать запись в `audit_log`. Debezium connector может подписаться на таблицу и отдавать события в Kafka для `ChangeLogMonitor.DataAggregator`.

## Таблица audit_log

| Колонка        | Тип                | Описание                                               |
|----------------|--------------------|--------------------------------------------------------|
| `id`           | `bigint`           | Identity, PK                                           |
| `transaction_id` | `varchar(255)`   | Уникальный ID транзакции (`yyyyMMddHHmmss-guid`)       |
| `payload`      | `bytea`            | Сериализованный `AuditMetaEnvelope`                    |
| `created_at`   | `timestamptz`      | Момент фиксации изменений (UTC)                        |
| `processed_at` | `timestamptz?`     | Заполняется downstream-консьюмерами (агрегаторы и т.д.) |

`payload` содержит актёра, контекст запроса, подсказки (`hints`) и другие данные, которые возвращает `IAuditMetadataProvider`.

## Кастомизация метаданных

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
    public string? GetServiceName() => "Billing.Api";
    public string? GetClientIp() =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? GetUserAgent() =>
        _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
    public Dictionary<string, string>? GetHints() => new()
    {
        ["featureFlag"] = "beta-editor"
    };
}
```

Передайте фабрику в `AddChangeLogInterceptor` или зарегистрируйте реализацию напрямую:

```csharp
builder.Services.AddScoped<IAuditMetadataProvider, HttpContextAuditMetadataProvider>();
```

## Взаимодействие с конфигурацией

- `ChangeLogInterceptor` использует `IAuditConfigurationService.IsEntityEnabled` и связанные методы, чтобы понимать, какие сущности попадут в журнал.
- Конфигурация YAML общая для всех модулей, поэтому важно держать её консистентной — UI и Aggregator используют те же правила отображения/маскирования.
- Подробности о схеме YAML и доступных пресетах описаны в `ChangeLogMonitor.Configuration/README.md`.

## Тесты

Интеграционные тесты расположены в `ChangeLogMonitor.Interceptor.Tests`. Они запускают PostgreSQL 16-alpine через Testcontainers и проверяют:

- режимы whitelist/blacklist конфигурации;
- целостность транзакций (интерцептор коммитит/ролбэчит синхронно с основным контекстом);
- корректность формируемого payload и сериализации protobuf.

Запуск:

```bash
dotnet test ChangeLogMonitor.Interceptor.Tests
```

Требуется доступный Docker daemon.

## Связанные модули

- `ChangeLogMonitor.Configuration` — загрузка и валидация YAML-политики;
- `ChangeLogMonitor.DataAggregator` — потребление CDC событий из Debezium и объединение с payload из `audit_log`;
- `ChangeLogMonitor.UI` — визуализация журнала для пользователей.
