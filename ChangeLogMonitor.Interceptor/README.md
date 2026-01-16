# ChangeLogMonitor.Interceptor

EF Core интерцептор для захвата изменений при `SaveChanges`.

**Возможности:**
- Перехват SaveChanges/SaveChangesAsync
- Применение YAML-политики
- Сериализация метаданных в protobuf
- Запись в `audit_log` в той же транзакции

**Использование:**
```csharp
builder.Services.AddChangeLogInterceptor(
    auditDbConnectionString: connectionString,
    configFilePath: "changelog-config.yaml",
    applyMigrations: true);

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddChangeLogInterceptor(sp);
});
```
