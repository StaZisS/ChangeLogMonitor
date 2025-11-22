# ChangeLogMonitor.TestHarness

Небольшой ASP.NET Core сервис (net9.0), который используется как тестовый стенд для отработки изменений в основном
решении.

## Возможности

- Таблицы `Users` и `Orders` в SQLite (`test-harness.db` в корне проекта).
- Предсозданный пользователь:
    - **Логин:** `demo-user`
    - **Пароль:** `demo-pass`
- Ручка `POST /auth/token` выдает JWT по паре логин/пароль.
- Ручка `POST /orders` создает заказ от текущего аутентифицированного пользователя; защищена JWT.

## Запуск

```bash
dotnet run --project ChangeLogMonitor.TestHarness
```

После старта Swagger UI будет доступен на `https://localhost:5001/swagger` (порт зависит от выбранного профиля).

## Docker стенд с Kafka/Debezium

1. Поднимите инфраструктуру из каталога `ChangeLogMonitor.TestHarness`:
   ```bash
   docker compose up -d
   ```
   Запустятся Postgres (`changelog`), Kafka (KRaft, без ZooKeeper), Debezium Connect, init-контейнер для регистрации
   коннектора и Kafka UI (`http://localhost:8080`).
   Kafka доступна с хоста на `localhost:9094`, а внутри docker-сети по `kafka:9092`.
2. Дайте сервису строку подключения к Postgres (например, через переменную окружения):
   ```bash
   ConnectionStrings__TestHarnessDb=Host=localhost;Port=5432;Database=changelog;Username=harness_app;Password=harness_pass
   ```
3. Коннектор Debezium (`changelog-test-harness`) применится автоматически init-контейнером. Если нужно
   переустановить/обновить конфигурацию вручную:
   ```bash
   curl -i -X PUT http://localhost:8083/connectors/changelog-test-harness/config \
     -H "Content-Type: application/json" \
     --data @debezium-connector.json
   ```
   Файл `debezium-connector.json` содержит только секцию `config` (имя задается в URL) и использует публикацию
   `changelog_publication` (`FOR ALL TABLES`). SMT `RegexRouter` сводит все таблицы в один топик `changelog.all` вместо
   отдельных (`changelog.public.users`, `orders`, `audit_log`). Включено `provide.transaction.metadata=true`, чтобы
   Debezium отправлял блок `transaction` – агрегатор использует его для `tx_id`. Можно переиспользовать init-контейнер
   командой `docker compose up debezium-init`.

4. Чтобы проверить ChangeLog Interceptor и аудит:
    - Передайте Postgres-строки для приложения и аудита (обычно одинаковые):
      ```bash
      ConnectionStrings__TestHarnessDb=Host=localhost;Port=5432;Database=changelog;Username=harness_app;Password=harness_pass
      ConnectionStrings__AuditDb=Host=localhost;Port=5432;Database=changelog;Username=harness_app;Password=harness_pass
      ```
    - В корне лежит `changelog-config.yaml` (whitelist для `User`/`Order`). При старте создаются `Users`, `Orders` и
      `audit_log`; при изменениях в API в `audit_log` пишется метаданные, Debezium транслирует их в Kafka.

Kafka UI (кластер `local`, Connect `debezium`) позволяет просматривать сообщения Debezium и состояние коннектора.

> По умолчанию сервис использует SQLite (`Data Source=test-harness.db`). Если строка подключения содержит `Host=` —
> будет подключение к Postgres через Npgsql и создание таблиц там.

Если вы уже поднимали Postgres и видите ошибку `permission denied for database changelog` в логе Debezium, выполните
миграцию прав вручную:

```bash
docker exec -it changelog-postgres psql -U postgres -d postgres -c "GRANT CREATE ON DATABASE changelog TO debezium;"
# затем обновите коннектор:
docker compose up debezium-init
```

Публикация `changelog_publication` создается в init-скрипте (drop/create, `FOR ALL TABLES`), а в коннекторе автосоздание
публикации отключено (`publication.autocreate.mode=disabled`), чтобы избежать ошибок владения таблицами.
Если публикации нет (старый volume), создайте вручную:

```bash
docker exec -it changelog-postgres psql -U postgres -d changelog \
  -c "DROP PUBLICATION IF EXISTS changelog_publication; CREATE PUBLICATION changelog_publication FOR ALL TABLES;"
docker compose run --rm debezium-init
```

Таблица `audit_log` создается при старте: модуль интерцептора применяет свою EF-миграцию. Если база уже была поднята
раньше и таблицы нет, можно создать вручную:

```bash
docker exec -it changelog-postgres psql -U postgres -d changelog \
  -c "CREATE TABLE IF NOT EXISTS audit_log (id BIGSERIAL PRIMARY KEY, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), processed_at TIMESTAMPTZ NULL, transaction_id VARCHAR(255) NOT NULL, payload BYTEA NOT NULL);
      CREATE INDEX IF NOT EXISTS idx_audit_log_transaction_id ON audit_log (transaction_id);
      CREATE INDEX IF NOT EXISTS idx_audit_log_created_at ON audit_log (created_at);
      CREATE INDEX IF NOT EXISTS idx_audit_log_processed_at ON audit_log (processed_at);"
```

## Примеры запросов

Файл [`ChangeLogMonitor.TestHarness.http`](ChangeLogMonitor.TestHarness.http) содержит готовые примеры:

1. Получить токен для `demo-user`.
2. Создать заказ, передав заголовок `Authorization: Bearer <token>`.

## Настройки

- JWT параметры лежат в `appsettings.json` (секция `Jwt`).
- Строка подключения настраивается через `ConnectionStrings:TestHarnessDb`.
- Для аудита/интерцептора используйте `ConnectionStrings:AuditDb` (если не задана, берется `TestHarnessDb`).

Базе данных инициализируется автоматически при старте (создается файл и добавляется пользователь, если его еще нет).
