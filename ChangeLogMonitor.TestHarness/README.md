# ChangeLogMonitor.TestHarness

Небольшой ASP.NET Core сервис (net9.0), который используется как тестовый стенд для отработки изменений в основном решении.

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

## Примеры запросов

Файл [`ChangeLogMonitor.TestHarness.http`](ChangeLogMonitor.TestHarness.http) содержит готовые примеры:

1. Получить токен для `demo-user`.
2. Создать заказ, передав заголовок `Authorization: Bearer <token>`.

## Настройки

- JWT параметры лежат в `appsettings.json` (секция `Jwt`).
- Строка подключения настраивается через `ConnectionStrings:TestHarnessDb`.

Базе данных инициализируется автоматически при старте (создается файл и добавляется пользователь, если его еще нет).
