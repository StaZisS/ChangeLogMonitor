# ChangeLogMonitor - Конфигурация

Конфигурация разделена на два файла:
- **changelog-config.yaml** - политика аудита (что логировать)
- **appsettings.json** - инфраструктура (как подключаться)

## Содержание

1. [Политика аудита (YAML)](#политика-аудита-yaml)
2. [Инфраструктурные настройки (JSON)](#инфраструктурные-настройки-json)
3. [Справочник полей YAML](#справочник-полей-yaml)

---

## Политика аудита (YAML)

Файл `changelog-config.yaml` определяет:
- Какие сущности/таблицы логировать
- Какие поля маскировать/шифровать/хешировать
- Как отображать ссылки (FK) и коллекции
- Правила доступа к аудит-логам

### Базовая структура

```yaml
auditPolicy:
  version: "1.0"
  mode: whitelist              # whitelist | blacklist

  onCreate: eventOnly          # eventOnly | allFields | nonDefaultFields
  onUpdate: delta              # delta | fullSnapshot
  onDelete: eventOnly          # eventOnly | allFields

  globalFieldExclusions:       # Глобально исключенные поля
    - RowVersion
    - ConcurrencyToken

  methodPresets:               # Пресеты для методов
    mask: { ... }
    hash: { ... }
    encrypt: { ... }

  referencePresets: { ... }    # Пресеты для ссылок
  collectionPresets: { ... }   # Пресеты для коллекций

  referenceDefaults: { ... }   # Умолчания для ссылок
  collectionDefaults: { ... }  # Умолчания для коллекций

  entities:                    # Настройки сущностей
    User: { ... }
    Order: { ... }

  accessControl: { ... }       # Контроль доступа

  defaultCulture: "ru-RU"
  defaultTimeZone: "Asia/Almaty"
```

### Настройки сущности

```yaml
entities:
  User:
    enabled: true
    onCreate: allFields
    onUpdate: delta
    onDelete: eventOnly

    access:
      allowedRoles: [admin, auditor]

    fields:
      Password: exclude
      Email:
        action: mask
        mask:
          preset: email

    references:
      DepartmentId:
        preset: fk_verbose
        nameSelector: "Department.Name"

    collections:
      Roles:
        preset: delta_verbose
        itemNameSelector: "Role.Name"
```

---

## Поля (`fields`)

### Короткий синтаксис

```yaml
fields:
  Password: exclude
  Email: mask
  SSN: hash
  CreditCard: encrypt
  Name: include
```

### Длинный синтаксис

```yaml
fields:
  Email:
    action: mask
    mask:
      preset: email
      char: "*"
      keepLeft: 2
      keepRight: 2
      preserveDomain: true
```

### Действия (action)

| Action | Описание |
|--------|----------|
| `exclude` | Полностью исключить из аудита |
| `include` | Включить с форматированием view |
| `mask` | Маскирование (частичное скрытие) |
| `hash` | Хеширование (для проверки равенства) |
| `encrypt` | Шифрование (обратимое) |

### Параметры маскирования (mask)

```yaml
mask:
  preset: email              # Имя пресета
  char: "*"                  # Символ маски
  keepLeft: 2                # Оставить символов слева
  keepRight: 2               # Оставить символов справа
  preserveDomain: true       # Сохранить домен (для email)
  preserveFormat: true       # Сохранить формат
  regex: null                # Regex для custom
  replace: null              # Замена для custom
```

**Результат:** `us***@domain.com`

### Параметры хеширования (hash)

```yaml
hash:
  algo: SHA-256              # SHA-256 | SHA-512 | BLAKE2b | Argon2id
  salt:
    strategy: per-record     # none | fixed | per-entity | per-field | per-record | per-tenant
    ref: kms:alias/audit-salt
  pepperRef: env:AUDIT_PEPPER
  encoding: base64           # hex | base64
  storeRaw: false            # Хранить оригинал
  storeHash: true            # Хранить хеш
  equalityToken: true        # Токен для сравнения
```

### Параметры шифрования (encrypt)

```yaml
encrypt:
  algo: AES-256-GCM          # AES-256-GCM | CHACHA20-POLY1305
  keyRef: kms:alias/audit-log
  aad: [entity, field, timestamp]
  iv:
    strategy: random         # random | derived
    store: true
    length: 12
  encoding: base64
  storeRaw: false
  rotate:
    enabled: true
    policy: by-key-alias     # by-key-alias | by-date
```

### Форматирование view

```yaml
fields:
  BirthDate:
    action: include
    view:
      format: date           # date | datetime | money | number | boolean | string
      pattern: "dd.MM.yyyy"
      culture: "ru-RU"

  # Enum поля автоматически определяются из EF Core модели
  # format: enum и enumType указывать НЕ нужно!
  Status:
    action: include
    # Тип enum автоматически определяется из CLR типа свойства
```

---

## Пресеты методов (`methodPresets`)

Определите один раз, используйте многократно:

```yaml
methodPresets:
  mask:
    email:
      char: "*"
      keepLeft: 2
      keepRight: 2
      preserveDomain: true

    phone:
      char: "*"
      keepLeft: 3
      keepRight: 2
      preserveFormat: true

  hash:
    sha256_salted:
      algo: SHA-256
      salt:
        strategy: per-record
        ref: kms:alias/audit-salt
      encoding: base64
      storeRaw: false
      storeHash: true

  encrypt:
    aes_gcm_default:
      algo: AES-256-GCM
      keyRef: kms:alias/audit-log
      encoding: base64
```

**Использование:**

```yaml
fields:
  Email:
    action: mask
    mask:
      preset: email
```

---

## Ссылки (`references`)

Денормализация FK - сохранение ключа и имени связанного объекта.

```yaml
references:
  DepartmentId:
    preset: fk_verbose
    showKey: true
    showName: true
    viewTemplate: "{name} (ID={key})"
    nameSelector: "Department.Name"
    nameResolve:
      stage: normalization   # raw | normalization
      fallback: "<Unknown> (ID={key})"
      maxLen: 256
    nameMaskPreset: human_name_mask
    nullTransitions: log     # log | skip
    changedAs: pair          # pair | verb
```

### Пресеты ссылок

```yaml
referencePresets:
  fk_verbose:
    showKey: true
    showName: true
    viewTemplate: "{name} (ID={key})"

  fk_compact:
    showKey: false
    showName: true
    viewTemplate: "{name}"

  fk_key_only:
    showKey: true
    showName: false
    viewTemplate: "{key}"
```

---

## Коллекции (`collections`)

Отслеживание дельт (добавлено/удалено).

```yaml
collections:
  Roles:
    preset: delta_verbose
    logDeltas: true
    showKeys: true
    showNames: true
    itemKeySelector: "Role.Id"
    itemNameSelector: "Role.Name"
    itemViewTemplate: "{name} (ID={key})"
    deltaView:
      addedPrefix: "Добавлено:"
      removedPrefix: "Удалено:"
      joiner: ", "
      collapseToCounters: false
    limits:
      addedMax: 200
      removedMax: 200
    countOnlyWhenLarge:
      enabled: true
      threshold: 2000
    trackReordering: false
    includeOnCreate: none    # none | all | limited
    includeOnDelete: none    # none | all
```

### Пресеты коллекций

```yaml
collectionPresets:
  delta_verbose:
    logDeltas: true
    showKeys: true
    showNames: true
    itemViewTemplate: "{name} (ID={key})"

  delta_compact:
    logDeltas: true
    showKeys: false
    showNames: true
    itemViewTemplate: "{name}"

  count_only:
    logDeltas: true
    showKeys: false
    showNames: false
    collapseToCounters: true
```

---

## Контроль доступа (`accessControl`)

Фильтрация доступа к истории аудита по ролям.

```yaml
accessControl:
  enabled: false
  unauthorizedBehavior: deny  # deny | filter

  roles:
    admin:
      description: "Полный доступ"
      allowAll: true

    auditor:
      description: "Просмотр всех сущностей"

    user:
      description: "Только свои записи"

  users:
    "user-id-1":
      roles: [admin]
    "user-id-2":
      roles: [auditor, manager]

  defaultRoles: [user]
  allowAnonymous: false
  anonymousRoles: []
```

---

## Инфраструктурные настройки (JSON)

Файл `appsettings.json` содержит настройки подключений.

### Полная структура

```json
{
  "ConnectionStrings": {
    "ApplicationDatabase": "Host=localhost;Port=5432;Database=myapp;Username=user;Password=pass",
    "AuditDatabase": "Host=localhost;Port=5432;Database=myapp_audit;Username=user;Password=pass"
  },

  "ChangeLogMonitor": {
    "Kafka": {
      "BootstrapServers": "localhost:9092",
      "SecurityProtocol": "Plaintext",
      "SaslMechanism": "Plain",
      "SaslUsername": "",
      "SaslPassword": "",
      "Topics": {
        "CdcEvents": "dbserver1.public.changelog",
        "Metadata": "dbserver1.public.changelog_metadata"
      },
      "ConsumerGroup": "changelog-monitor-group",
      "AutoOffsetReset": "Earliest",
      "EnableAutoCommit": false
    },

    "Debezium": {
      "ServerName": "dbserver1",
      "ConnectorName": "changelog-connector",
      "DatabaseHostname": "localhost",
      "DatabasePort": 5432,
      "DatabaseName": "myapp",
      "DatabaseUser": "debezium_user",
      "DatabasePassword": "debezium_password",
      "TableIncludeList": "public.users,public.orders",
      "PluginName": "pgoutput"
    },

    "Storage": {
      "Provider": "PostgreSQL",
      "TablePrefix": "audit_",
      "SchemaName": "audit",
      "EnableCompression": true,
      "EnableEncryption": false
    },

    "ConfigFile": "changelog-config.yaml"
  },

  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "ChangeLogMonitor": "Debug"
    }
  },

  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://localhost:5000" },
      "Https": { "Url": "https://localhost:5001" }
    }
  }
}
```

### DataAggregator настройки

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
      "DlqTopic": "aggregator.dlq",
      "EnableAutoTopicCreation": false
    },
    "Processing": {
      "FlushIntervalMs": 800,
      "HardTtlMs": 2000,
      "MaxEventsPerBucket": 1000,
      "RejectWithoutTxId": true,
      "EmitPartialOnLimit": true
    },
    "Http": {
      "Port": 8080
    }
  }
}
```

### Finalization настройки

```json
{
  "App": {
    "Kafka": {
      "BootstrapServers": "localhost:9092",
      "InputTopic": "aggregates.by_tx",
      "GroupId": "changemonitor-finalizer",
      "EnableAutoCreateTopics": false
    },
    "ClickHouse": {
      "ConnectionString": "Host=localhost;Port=9000;Database=default;User=default;Password=",
      "TableName": "audit_log",
      "EnsureSchema": true
    },
    "Http": {
      "Port": 8081
    }
  }
}
```

---

## Справочник полей YAML

### Приоритет правил

1. `exclude` отменяет все другие действия
2. Среди методов хранения raw выбирается один: `encrypt` или `hash` или `include`
3. Настройки `view` определяют форматирование
4. Локальные настройки > пресет > умолчания

### Raw vs View

Для каждого изменённого поля хранится:
- **Raw** - сырое значение (может быть зашифровано/хешировано)
- **View** - человекочитаемое представление

### Независимость аудита

- В таблицах аудита нет FK на бизнес-схему
- Сохраняются полные текстовые лейблы/заголовки
- История не ломается при миграциях/переименованиях

### Двухфазная запись

1. **Сырые события** - записываются при `SaveChanges`
2. **Финальная нормализация** - обогащение view-лейблами (фоном)

### Умолчания

```yaml
referenceDefaults:
  showKey: true
  showName: true
  viewTemplate: "{name} (ID={key})"
  nameResolve:
    stage: normalization
    fallback: "{key}"
    maxLen: 256

collectionDefaults:
  logDeltas: true
  showKeys: true
  showNames: true
  itemViewTemplate: "{name} (ID={key})"
  limits:
    addedMax: 200
    removedMax: 200
```

---

## Полный пример конфигурации

```yaml
auditPolicy:
  version: "1.0"
  mode: whitelist
  onCreate: eventOnly
  onUpdate: delta
  onDelete: eventOnly

  globalFieldExclusions:
    - RowVersion
    - ConcurrencyToken

  methodPresets:
    mask:
      email:
        char: "*"
        keepLeft: 2
        keepRight: 2
        preserveDomain: true

    hash:
      sha256_salted:
        algo: SHA-256
        salt:
          strategy: per-record
          ref: kms:alias/audit-salt
        encoding: base64
        storeHash: true

  referencePresets:
    fk_verbose:
      showKey: true
      showName: true
      viewTemplate: "{name} (ID={key})"

  collectionPresets:
    delta_verbose:
      logDeltas: true
      showKeys: true
      showNames: true

  entities:
    User:
      enabled: true
      onCreate: allFields

      fields:
        Password: exclude
        Email:
          action: mask
          mask:
            preset: email
        BirthDate:
          action: include
          view:
            format: date
            pattern: "dd.MM.yyyy"

      references:
        DepartmentId:
          preset: fk_verbose
          nameSelector: "Department.Name"

      collections:
        Roles:
          preset: delta_verbose
          itemNameSelector: "Role.Name"

  defaultCulture: "ru-RU"
  defaultTimeZone: "Asia/Almaty"
```

---

## Рекомендации

1. **Секреты** - храните только ссылки на KMS/Secret Manager (`ref`, `keyRef`, `pepperRef`)
2. **Большие коллекции** - используйте `limits` и `countOnlyWhenLarge`
3. **Дорогие операции** - выносите на этап нормализации (`stage: normalization`)
4. **PII данные** - используйте `mask`, `hash` или `encrypt`
5. **Независимость** - не создавайте FK из аудита на бизнес-схему
