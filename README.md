# ChangeLogMonitor - Документация

Cистема логирования изменений данных.

## Документация

- [Конфигурация](CONFIGURATION.md) - настройка политики аудита и инфраструктуры
- [Настройка CDC](SETUP-CDC.md) - настройка PostgreSQL и Debezium для CDC

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

### Standalone (отдельный сервис)

Разверните `ChangeLogMonitor.Standalone` как отдельный сервис.

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

   # TestProject тестовый проект с готовыми эндпоинтами для проверки функционала
   dotnet run --project TestProject
   ```

---

## Требования

- .NET 9.0
- Entity Framework Core 9
- Kafka
- Debezium
- ClickHouse
- PostgreSQL(и другие базы поддерживающие WAL)

---

## Помощь в конфигурации

Для помощи в синтаксисе созданы подсказки в rider

Для активации необходимо сделать следующие шаги

```
 1. Settings → Languages & Frameworks → Schemas and DTDs → JSON Schema Mappings                                                                                                                                                                                                                                 
 2. Нажать +                                                                                                                                                                                                                                                                                                    
 3. Заполнить:                                                                                                                                                                                                                                                                                                  
   - Name: ChangeLogMonitor Config                                                                                                                                                                                                                                                                              
   - Schema file or URL: выбрать changelog-config.schema.yaml                                                                                                                                                                                                                                                   
   - Нажать + в нижней части и добавить File path pattern: changelog-config*.yaml                                                                                                                                                                                                                               
 4. Apply → OK 
```
