# ChangeLogMonitor.Configuration

Модуль для работы с YAML конфигурацией политики аудита.

## Назначение

Модуль обрабатывает YAML конфигурацию (`changelog-config.yaml`), которая определяет:
- **Какие сущности** логировать (whitelist/blacklist)
- **Какие поля** включать в аудит
- **Чувствительные данные**: маскирование, хеширование, шифрование
- **Ссылки (FK)**: денормализация имен связанных объектов
- **Коллекции**: отслеживание дельт (добавлено/удалено)
- **Форматирование**: view-представления для UI (даты, деньги, enum)

## Структура

```
ChangeLogMonitor.Configuration/
├── Models/                    # YAML модели для десериализации
│   ├── YamlAuditPolicyRoot.cs
│   ├── YamlEntityPolicy.cs
│   ├── YamlFieldPolicy.cs
│   ├── YamlReferencePolicy.cs
│   ├── YamlCollectionPolicy.cs
│   └── YamlPresets.cs
├── Providers/                 # Провайдеры для загрузки YAML
│   └── YamlAuditPolicyProvider.cs
├── Validators/                # Валидаторы FluentValidation
│   └── YamlAuditPolicyValidator.cs
├── Mappers/                   # Конвертация YAML → Domain модели
│   └── AuditPolicyMapper.cs
├── Services/                  # Главный сервис конфигурации
│   └── AuditConfigurationService.cs
└── Extensions/                # DI Extensions
    └── ServiceCollectionExtensions.cs
```

## Использование

### 1. Регистрация в DI контейнере

```csharp
using ChangeLogMonitor.Configuration.Extensions;

// В Program.cs или Startup.cs
builder.Services.AddAuditConfiguration(); // По умолчанию ищет changelog-config.yaml

// Или с указанием пути
builder.Services.AddAuditConfiguration("path/to/my-config.yaml");

// Или через опции
builder.Services.AddAuditConfiguration(options =>
{
    options.ConfigFilePath = "custom-path/changelog-config.yaml";
});
```

### 2. Использование в коде

```csharp
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Core.Models.Policy;

public class MyService
{
    private readonly IAuditConfigurationService _configService;

    public MyService(IAuditConfigurationService configService)
    {
        _configService = configService;
    }

    public void Example()
    {
        // Получить полную политику аудита
        AuditPolicy policy = _configService.GetPolicy();

        // Получить политику для конкретной сущности
        EntityPolicy? userPolicy = _configService.GetEntityPolicy("User");

        // Проверить, включена ли сущность
        bool isEnabled = _configService.IsEntityEnabled("User");

        // Получить информацию о конфигурации
        var info = _configService.GetConfigurationInfo();
        Console.WriteLine($"Version: {info.Version}, Entities: {info.EntityCount}");

        // Принудительно перезагрузить конфигурацию
        _configService.ReloadConfiguration();
    }
}
```

### 3. Создание конфигурации

Скопируйте `changelog-config.example.yaml` в корень проекта как `changelog-config.yaml`:

```bash
cp changelog-config.example.yaml changelog-config.yaml
```

Пример минимальной конфигурации:

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
        FirstName: include
```

## Ключевые возможности

### Короткий и длинный синтаксис

```yaml
fields:
  # Короткий синтаксис
  Password: exclude
  Email: include

  # Длинный синтаксис
  BirthDate:
    action: include
    view:
      format: date
      pattern: "dd.MM.yyyy"
```

### Пресеты

Определите пресеты один раз и используйте многократно:

```yaml
methodPresets:
  mask:
    email:
      char: "*"
      keepLeft: 2
      keepRight: 2
      preserveDomain: true

entities:
  User:
    fields:
      Email:
        action: mask
        mask:
          preset: email  # Использование пресета
```

### Маскирование, хеширование, шифрование

```yaml
fields:
  # Маскирование (для view)
  Email:
    action: mask
    mask:
      preset: email

  # Хеширование (для проверки равенства без раскрытия)
  SSN:
    action: hash
    hash:
      algo: SHA-256
      storeHash: true
      storeRaw: false

  # Шифрование (обратимое)
  CreditCardNumber:
    action: encrypt
    encrypt:
      algo: AES-256-GCM
      keyRef: kms:alias/audit-key
```

### Ссылки (FK)

Автоматическая денормализация имен связанных объектов:

```yaml
references:
  DepartmentId:
    showKey: true
    showName: true
    viewTemplate: "{name} (ID={key})"
    nameSelector: "Department.Name"
    nameResolve:
      stage: normalization  # Резолвится фоном
      fallback: "<Unknown> (ID={key})"
```

### Коллекции

Отслеживание дельт (добавлено/удалено):

```yaml
collections:
  Roles:
    logDeltas: true
    itemNameSelector: "Role.Name"
    deltaView:
      addedPrefix: "Добавлена роль:"
      removedPrefix: "Удалена роль:"
    limits:
      addedMax: 50
      removedMax: 50
```

## Валидация

Модуль автоматически валидирует конфигурацию при загрузке:

- Проверка допустимых значений enum (mode, onCreate, onUpdate и т.д.)
- Проверка шаблонов (viewTemplate должен содержать `{name}` или `{key}`)
- Проверка лимитов (> 0)
- Валидация зависимостей (если action=mask, то mask настройки обязательны)

При ошибке валидации выбрасывается `ValidationException` с подробным описанием.

## Кеширование

Конфигурация загружается один раз при первом обращении и кешируется. Для перезагрузки:

```csharp
_configService.ReloadConfiguration();
```

Или через force reload:

```csharp
var policy = _configService.GetPolicy(forceReload: true);
```

## Документация

Подробное описание всех полей и возможностей: `audit-policy-fields-doc.md`

## Зависимости

- **ChangeLogMonitor.Core** - доменные модели
- **YamlDotNet** - парсинг YAML
- **FluentValidation** - валидация конфигурации
- **Microsoft.Extensions.Options** - интеграция с .NET Options pattern
