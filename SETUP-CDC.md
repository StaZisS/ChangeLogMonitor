# Настройка CDC (Change Data Capture)

Пошаговое руководство по настройке PostgreSQL logical replication и Debezium для ChangeLogMonitor.

## Содержание

1. [Обзор](#обзор)
2. [Настройка PostgreSQL](#настройка-postgresql)
3. [Настройка Debezium](#настройка-debezium)
4. [Проверка работоспособности](#проверка-работоспособности)
5. [Troubleshooting](#troubleshooting)

---

## Обзор

### Требования

- PostgreSQL 10+ (рекомендуется 14+)
- Kafka + Kafka Connect
- Debezium PostgreSQL Connector 2.x
- Права суперпользователя или REPLICATION для настройки

### Что настраивается

| Компонент | Что делает | Кто настраивает |
|-----------|------------|-----------------|
| `wal_level=logical` | Включает logical replication | DBA (postgresql.conf) |
| Пользователь Debezium | Читает WAL и таблицы | DBA (один раз) |
| `REPLICA IDENTITY FULL` | Включает OLD значения в WAL | DBA (для каждой таблицы) |
| `PUBLICATION` | Определяет таблицы для репликации | DBA (один раз) |
| Debezium Connector | Читает WAL и отправляет в Kafka | DevOps (при деплое) |

---

## Настройка PostgreSQL

### Шаг 1: Проверка/включение logical replication

Проверьте текущее значение:

```sql
SHOW wal_level;
```

Если не `logical`, измените в `postgresql.conf`:

```ini
wal_level = logical
max_wal_senders = 10
max_replication_slots = 10
```

Или через `ALTER SYSTEM` (требуется перезапуск):

```sql
ALTER SYSTEM SET wal_level = 'logical';
ALTER SYSTEM SET max_wal_senders = 10;
ALTER SYSTEM SET max_replication_slots = 10;
```

После изменения перезапустите PostgreSQL:

```bash
sudo systemctl restart postgresql
```

---

### Шаг 2: Создание пользователя для Debezium

Создайте пользователя с минимально необходимыми правами:

```sql
CREATE USER debezium WITH REPLICATION LOGIN PASSWORD 'your_secure_password';

GRANT CONNECT ON DATABASE your_database TO debezium;

GRANT USAGE ON SCHEMA public TO debezium;

GRANT SELECT ON ALL TABLES IN SCHEMA public TO debezium;

ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO debezium;
```

---

### Шаг 3: Настройка REPLICA IDENTITY

По умолчанию PostgreSQL записывает в WAL только PRIMARY KEY для UPDATE/DELETE. Для получения старых значений полей нужен `REPLICA IDENTITY FULL`:

```sql
ALTER TABLE public.users REPLICA IDENTITY FULL;
ALTER TABLE public.orders REPLICA IDENTITY FULL;
ALTER TABLE public.order_items REPLICA IDENTITY FULL;
ALTER TABLE public.audit_log REPLICA IDENTITY FULL;
-- ... добавьте все таблицы, которые хотите отслеживать
```

**Проверка текущих настроек:**

```sql
SELECT
    c.relname AS table_name,
    CASE c.relreplident
        WHEN 'd' THEN 'default (PK)'
        WHEN 'n' THEN 'nothing'
        WHEN 'f' THEN 'FULL'
        WHEN 'i' THEN 'index'
    END AS replica_identity
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relkind = 'r'
ORDER BY c.relname;
```

**Массовая установка для всех таблиц:**

```sql
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN
        SELECT tablename
        FROM pg_tables
        WHERE schemaname = 'public'
    LOOP
        EXECUTE format('ALTER TABLE public.%I REPLICA IDENTITY FULL', r.tablename);
        RAISE NOTICE 'Set REPLICA IDENTITY FULL for %', r.tablename;
    END LOOP;
END $$;
```

---

### Шаг 4: Создание публикации

Публикация определяет, какие таблицы будут отслеживаться:

**Вариант A: Конкретные таблицы (рекомендуется)**

```sql
CREATE PUBLICATION changelog_publication FOR TABLE
    public.users,
    public.orders,
    public.order_items,
    public.audit_log;
```

**Вариант B: Все таблицы схемы**

```sql
CREATE PUBLICATION changelog_publication FOR ALL TABLES;
```

**Добавление/удаление таблиц из публикации:**

```sql
-- Добавить таблицу
ALTER PUBLICATION changelog_publication ADD TABLE public.new_table;

-- Удалить таблицу
ALTER PUBLICATION changelog_publication DROP TABLE public.old_table;
```

**Проверка публикации:**

```sql
-- Список публикаций
SELECT * FROM pg_publication;

-- Таблицы в публикации
SELECT * FROM pg_publication_tables WHERE pubname = 'changelog_publication';
```

---

### Шаг 5: Проверка настроек PostgreSQL

Выполните итоговую проверку:

```sql
-- 1. WAL level
SELECT name, setting FROM pg_settings WHERE name = 'wal_level';

-- 2. Пользователь Debezium
SELECT rolname, rolreplication FROM pg_roles WHERE rolname = 'debezium';

-- 3. Публикация
SELECT pubname, puballtables FROM pg_publication WHERE pubname = 'changelog_publication';

-- 4. Таблицы в публикации
SELECT schemaname, tablename FROM pg_publication_tables WHERE pubname = 'changelog_publication';

-- 5. REPLICA IDENTITY
SELECT c.relname, c.relreplident
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public' AND c.relkind = 'r';
```

---

## Настройка Debezium

### Шаг 1: Подготовка конфигурации коннектора

Создайте файл `debezium-connector.json`:

```json
{
  "connector.class": "io.debezium.connector.postgresql.PostgresConnector",

  "database.hostname": "your-postgres-host",
  "database.port": "5432",
  "database.user": "debezium",
  "database.password": "your_secure_password",
  "database.dbname": "your_database",

  "topic.prefix": "changelog",
  "slot.name": "changelog_slot",
  "publication.name": "changelog_publication",
  "publication.autocreate.mode": "disabled",

  "plugin.name": "pgoutput",
  "schema.include.list": "public",

  "include.schema.changes": "false",
  "tombstones.on.delete": "false",
  "decimal.handling.mode": "double",
  "time.precision.mode": "adaptive_time_microseconds",

  "heartbeat.interval.ms": "30000",
  "provide.transaction.metadata": "true",

  "key.converter": "org.apache.kafka.connect.json.JsonConverter",
  "value.converter": "org.apache.kafka.connect.json.JsonConverter",
  "key.converter.schemas.enable": "false",
  "value.converter.schemas.enable": "false",

  "transforms": "route",
  "transforms.route.type": "org.apache.kafka.connect.transforms.RegexRouter",
  "transforms.route.regex": ".*",
  "transforms.route.replacement": "changelog.all"
}
```

### Шаг 2: Регистрация коннектора

**Проверка готовности Kafka Connect:**

```bash
curl -s http://localhost:8083/ | jq .
```

**Регистрация коннектора:**

```bash
curl -X POST http://localhost:8083/connectors \
  -H "Content-Type: application/json" \
  -d @debezium-connector.json
```

Или через PUT (создание/обновление):

```bash
curl -X PUT http://localhost:8083/connectors/changelog-connector/config \
  -H "Content-Type: application/json" \
  -d @debezium-connector.json
```

---

### Шаг 3: Проверка коннектора

**Статус коннектора:**

```bash
curl -s http://localhost:8083/connectors/changelog-connector/status | jq .
```

Ожидаемый ответ:

```json
{
  "name": "changelog-connector",
  "connector": {
    "state": "RUNNING",
    "worker_id": "debezium:8083"
  },
  "tasks": [
    {
      "id": 0,
      "state": "RUNNING",
      "worker_id": "debezium:8083"
    }
  ],
  "type": "source"
}
```

**Список коннекторов:**

```bash
curl -s http://localhost:8083/connectors | jq .
```

---

## Проверка работоспособности

### 1. Проверка replication slot

```sql
SELECT slot_name, plugin, slot_type, active
FROM pg_replication_slots
WHERE slot_name = 'changelog_slot';
```

### 2. Проверка Kafka топиков

```bash
# Список топиков
kafka-topics --bootstrap-server localhost:9092 --list

# Чтение событий
kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic changelog.all \
  --from-beginning \
  --max-messages 5
```

### 3. Тестовое изменение

```sql
-- Создайте тестовое изменение
UPDATE public.users SET updated_at = NOW() WHERE id = 1;
```

Проверьте, что событие появилось в Kafka.

---

## Troubleshooting

### Коннектор не запускается

**Проверьте логи:**

```bash
docker logs changelog-debezium 2>&1 | tail -100
```

**Частые ошибки:**

| Ошибка | Причина | Решение |
|--------|---------|---------|
| `FATAL: no pg_hba.conf entry` | Нет доступа по сети | Добавьте правило в pg_hba.conf |
| `must be superuser or replication role` | Нет прав REPLICATION | `ALTER USER debezium REPLICATION` |
| `publication does not exist` | Публикация не создана | Создайте публикацию |
| `replication slot already exists` | Слот занят | Удалите старый слот |

### Удаление застрявшего слота

```sql
SELECT pg_drop_replication_slot('changelog_slot');
```

### Перезапуск коннектора

```bash
# Удалить
curl -X DELETE http://localhost:8083/connectors/changelog-connector

# Создать заново
curl -X POST http://localhost:8083/connectors \
  -H "Content-Type: application/json" \
  -d @debezium-connector.json
```

### Проверка прав пользователя

```sql
-- Права на таблицы
SELECT table_schema, table_name, privilege_type
FROM information_schema.role_table_grants
WHERE grantee = 'debezium';

-- Право REPLICATION
SELECT rolname, rolreplication FROM pg_roles WHERE rolname = 'debezium';
```

---

## Полный скрипт настройки PostgreSQL

Сохраните как `setup-cdc.sql` и выполните от имени суперпользователя:

```sql
-- 1. Создание пользователя Debezium
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'debezium') THEN
        CREATE USER debezium WITH REPLICATION LOGIN PASSWORD 'change_me_in_production';
        RAISE NOTICE 'User debezium created';
    ELSE
        RAISE NOTICE 'User debezium already exists';
    END IF;
END $$;

-- 2. Права на базу данных
GRANT CONNECT ON DATABASE current_database() TO debezium;
GRANT USAGE ON SCHEMA public TO debezium;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO debezium;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO debezium;

-- 3. REPLICA IDENTITY FULL для всех таблиц
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN
        SELECT tablename
        FROM pg_tables
        WHERE schemaname = 'public'
    LOOP
        EXECUTE format('ALTER TABLE public.%I REPLICA IDENTITY FULL', r.tablename);
        RAISE NOTICE 'REPLICA IDENTITY FULL: %', r.tablename;
    END LOOP;
END $$;

-- 4. Создание публикации
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_publication WHERE pubname = 'changelog_publication') THEN
        CREATE PUBLICATION changelog_publication FOR ALL TABLES;
        RAISE NOTICE 'Publication changelog_publication created';
    ELSE
        RAISE NOTICE 'Publication changelog_publication already exists';
    END IF;
END $$;

-- 5. Проверка
SELECT 'wal_level' AS check, setting AS value FROM pg_settings WHERE name = 'wal_level'
UNION ALL
SELECT 'replication_user', CASE WHEN rolreplication THEN 'OK' ELSE 'NO' END
FROM pg_roles WHERE rolname = 'debezium'
UNION ALL
SELECT 'publication', pubname FROM pg_publication WHERE pubname = 'changelog_publication';
```

---

## Docker Compose (для разработки)

Для локальной разработки используйте `TestProject/docker-compose.yml`, который уже включает преднастроенные PostgreSQL, Kafka и Debezium.

```bash
cd TestProject
docker-compose up -d
```

Этот стек использует образ `debezium/postgres:16`, где `wal_level=logical` уже включен.
