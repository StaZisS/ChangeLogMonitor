# ChangeLogMonitor.TestHarness

Тестовый стенд для разработки.

**Возможности:**
- SQLite по умолчанию, PostgreSQL через connection string
- Демо-пользователь: `demo-user` / `demo-pass`
- JWT: `POST /auth/token`
- Swagger UI: `/swagger`

**Запуск:**
```bash
dotnet run --project ChangeLogMonitor.TestHarness
```

**Docker стенд:**
```bash
cd ChangeLogMonitor.TestHarness
docker compose up -d
```

Подробная документация: [DOCUMENTATION.md](../DOCUMENTATION.md#changelogmonitortestharness)
