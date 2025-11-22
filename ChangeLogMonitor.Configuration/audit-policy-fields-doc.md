# Политика логирования аудита (YAML): справочник полей, ссылок и коллекций

Этот документ описывает **структуру и параметры** конфигурации политики логирования для модуля аудита в .NET/EF Core.
Документ ориентирован на **описание полей (`fields`)**, а также **ссылок (`references`)** и **коллекций (`collections`)
** — с поддержкой **пресетов** и **умолчаний**. Требования сведены из исходного документа по проекту (аудит:
кто/когда/что; было/стало; enum/ссылки/коллекции; независимость от бизнес‑схемы; чувствительные данные; двухфазная
нормализация и т. д.). fileciteturn1file5

> Ключевые ориентиры требований: логирование C/U/D; хранить “было/стало” c “raw+view”; для FK — ключ + имя; для
> коллекций — дельта “добавлен/удалён”; отсутствие FK из аудита на бизнес‑схему; гибкая политика (чёрные/белые списки);
> двухфазная нормализация “сырые → финальные”; формирование человекочитаемых сообщений с локалью/таймзоной.
> fileciteturn1file0turn1file1turn1file4turn1file2

---

## 1. Базовые понятия

- **Raw vs View**: для каждого изменённого поля в логе хранится **сырое значение** (raw) и **человекочитаемое** (view).
  Форматы по умолчанию настраиваемы (даты, числа и т. д.). fileciteturn1file0
- **FK и коллекции**: для **FK** записываем **ключ** и **денормализованное имя** (view). Для **коллекций** — **дельта**:
  добавленные/удалённые элементы с “чел.” представлением (без FK‑ссылок на бизнес‑таблицы). fileciteturn1file4
- **Независимость**: в таблицах аудита нет внешних ключей на бизнес‑схему; в лог сохраняются **полные текстовые
  заголовки/лейблы**, чтобы история не ломалась при миграциях/переименованиях. fileciteturn1file1
- **Политика**: поддерживаются **чёрные/белые списки**, глобальные и точечные правила (по сущностям/полям/связям).
  fileciteturn1file1
- **Двухфазная запись**: **сырые** события записываются при `SaveChanges`; **финальная нормализация** (обогащение
  view‑лейблами, шаблоны для UI) — фоном. fileciteturn1file1
- **UI‑сообщения**: шаблоны для Update/Create/Delete/Коллекций; локаль — русская, таймзона проекта (по умолчанию
  `Asia/Almaty`), хранение UTC. fileciteturn1file2

---

## 2. Приоритет правил (Resolution Order)

1) `exclude` **отменяет** любые другие действия;
2) среди методов хранения raw выбирается **ровно один**: `encrypt` **или** `hash` **или** `include`;
3) настройки `view` определяют формат **человекочитаемого** вывода (не зависят от выбора хранения raw);
4) локальные настройки сущности/поля **перекрывают** пресет; пресет **перекрывает** умолчания (defaults).

Это согласуется с требованием гибкой политики (+ двухфазной нормализации для формирования view‑значений).
fileciteturn1file1

---

## 3. Поля (`fields`)

### 3.1 Короткий и длинный синтаксис

- **Короткий**: когда достаточно дефолтов.
  ```yaml
  fields:
    Password: exclude
    Email: mask
    SSN: hash
    CreditCardNumber: encrypt
    Name: include
  ```
- **Длинный**: когда нужны параметры метода.
  ```yaml
  fields:
    SSN:
      action: hash
      hash:
        algo: SHA-256
        salt:
          strategy: per-record     # none|fixed|per-entity|per-field|per-record|per-tenant
          ref: kms:alias/audit-salt
        pepperRef: env:AUDIT_PEPPER
        encoding: base64
        storeRaw: false
        storeHash: true
        equalityToken: true
    Email:
      action: mask
      mask:
        preset: email
        char: "*"
        keepLeft: 2
        keepRight: 2
        preserveDomain: true
        preserveFormat: true
    CreditCardNumber:
      action: encrypt
      encrypt:
        algo: AES-256-GCM
        keyRef: kms:alias/audit-log
        aad: [entity, field, timestamp]
        iv: { strategy: random, store: true, length: 12 }
        encoding: base64
        storeRaw: false
        rotate: { enabled: true, policy: by-key-alias }
    BirthDate:
      action: include
      view: { format: date, pattern: "dd.MM.yyyy", culture: "ru-RU" }
    Password:
      action: exclude
  ```

### 3.2 `action: exclude`

**Полностью исключает поле из аудита** — ни raw, ни view не записываются. Рекомендовано для секретов (пароли, токены и
т. п.). fileciteturn1file2

### 3.3 `action: include` (+ `view`)

Хранение raw‑значения (без трансформации), при этом `view` управляет форматированием для UI:

```yaml
view:
  format: date | datetime | money | number | boolean | string
  pattern: "dd.MM.yyyy HH:mm"
  culture: "ru-RU"
  enumLabel: true       # для enum — писать подпись (лейбл)
  refName: true         # для FK — показывать денормализованное имя
```

- Для **enum** дополнительно в логе сохраняем **сырое значение** и **view‑лейбл** (на момент изменения).
  fileciteturn1file0

### 3.4 `action: mask` (маскирование view)

Параметры:

```yaml
mask:
  preset: email | phone | custom
  char: "*"
  keepLeft: 0
  keepRight: 0
  preserveDomain: false
  preserveFormat: true
  regex: null     # при custom
  replace: null   # при custom
```

Используйте, когда значение нужно **показать частично**, не раскрывая полностью (PII и т. п.). В сыром хранилище raw
остаётся согласно выбранной стратегии (обычно raw не пишем при маске для чувствительных полей). fileciteturn1file2

### 3.5 `action: hash` (хэширование raw)

Параметры:

```yaml
hash:
  algo: SHA-256 | SHA-512 | BLAKE2b | Argon2id
  argon2: { memoryKB: 65536, iterations: 3, parallelism: 1 }  # для Argon2id
  salt:
    strategy: none | fixed | per-entity | per-field | per-record | per-tenant
    ref: kms:alias/audit-salt
  pepperRef: env:AUDIT_PEPPER
  encoding: hex | base64
  storeRaw: false
  storeHash: true
  equalityToken: false
```

- Позволяет **верифицировать изменение без раскрытия** исходника (сравнение хэшей).
- Не храните соли/перцы в YAML — только **ссылки** (`ref`/`pepperRef`) на Secret Manager/KMS.
- Для «секретных» полей — `storeRaw: false`. fileciteturn1file2

### 3.6 `action: encrypt` (шифрование raw)

Параметры:

```yaml
encrypt:
  algo: AES-256-GCM | CHACHA20-POLY1305
  keyRef: kms:alias/audit-log
  aad: [ entity, field, timestamp ]
  iv: { strategy: random | derived, store: true, length: 12 }
  encoding: hex | base64
  storeRaw: false
  rotate: { enabled: true, policy: by-key-alias | by-date }
```

- Выбирайте AEAD‑алгоритмы (аутентифицированное шифрование); храните только **ссылку** на ключ (`keyRef`).
- `aad` связывает шифртекст с контекстом (целостность).
- Для длинной жизни ключей предусмотрена **ротация**. fileciteturn1file2

---

## 4. Пресеты для методов по полям (`methodPresets`)

Чтобы не дублировать параметры, используйте **пресеты** и ссылку `preset` в конкретном поле:

```yaml
methodPresets:
  mask:
    email:
      char: "*"
      keepLeft: 2
      keepRight: 2
      preserveDomain: true
      preserveFormat: true
    human_name_mask:
      char: "•"
      keepLeft: 1
      keepRight: 1
      preserveFormat: true

  hash:
    sha256_salted:
      algo: SHA-256
      salt: { strategy: per-record, ref: kms:alias/audit-salt }
      pepperRef: env:AUDIT_PEPPER
      encoding: base64
      storeRaw: false
      storeHash: true
      equalityToken: true

  encrypt:
    aes_gcm_default:
      algo: AES-256-GCM
      keyRef: kms:alias/audit-log
      aad: [ entity, field, timestamp ]
      iv: { strategy: random, store: true, length: 12 }
      encoding: base64
      storeRaw: false
```

Использование пресета в поле:

```yaml
fields:
  Email: { action: mask,   mask: { preset: email } }
  SSN: { action: hash,   hash: { preset: sha256_salted } }
  Card: { action: encrypt, encrypt: { preset: aes_gcm_default } }
```

---

## 5. Ссылки (`references`)

**Семантика:** одиночное ссылочное поле (FK) — модель изменения *A → B* (замена). В логах сохраняем **сырой ключ** и *
*имя** связанного объекта (**денормализовано** на момент изменения). Никаких FK из аудита на бизнес‑схему.
fileciteturn1file4

### 5.1 Параметры `references.<FkName>`

```yaml
references:
  DepartmentId:
    preset: fk_verbose            # необязательно: имя пресета
    showKey: true                 # писать ID
    showName: true                # писать имя (денормализованное)
    viewTemplate: "{name} (ID={key})"
    nameSelector: "Department.Name"
    nameResolve:
      stage: normalization        # raw | normalization
      fallback: "{key}"
      maxLen: 256
    nameMaskPreset: human_name_mask  # опциональная маска для имени
    key:
      treatAsSensitive: false
      maskPreset: null
      hashPreset: null
    nullTransitions: log          # log | skip
    changedAs: pair               # pair | verb
```

- `stage: normalization` позволяет резолвить имена **фоном** (двухфазная модель). fileciteturn1file1
- `viewTemplate` определяет UI‑вывод пары “имя+ID”.
- При отсутствии объекта по FK используйте `fallback` (история остаётся читабельной). fileciteturn1file1

### 5.2 Пресеты для ссылок (`referencePresets`)

```yaml
referencePresets:
  fk_verbose: { showKey: true,  showName: true,  viewTemplate: "{name} (ID={key})" }
  fk_compact: { showKey: false, showName: true,  viewTemplate: "{name}" }
  fk_key_only: { showKey: true,  showName: false, viewTemplate: "{key}" }
  fk_sensitive_name:
    showKey: true
    showName: true
    viewTemplate: "{maskedName} (ID={key})"
    nameMaskPreset: human_name_mask
```

---

## 6. Коллекции (`collections`)

**Семантика:** множество связанных объектов (1–* или *–*). Логируем **дельту**: списки `added[]` и `removed[]` (с
имёнами/ключами элементов), а не полный снимок каждый раз. В лог сохраняются **текстовые копии** (view) и ключи, **без
FK на бизнес‑схему**. fileciteturn1file4

### 6.1 Параметры `collections.<NavName>`

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
    includeOnCreate: none       # none | all | limited
    includeOnDelete: none       # none | all
    membershipKeysSensitive: false
    m2m:
      joinEntity: "UserRole"
      joinFields: [ "AssignedAt" ]
      treatJoinCudAsMembership: true
```

- Дельты повышают читаемость и уменьшают размер событий (“добавлен/удалён”). fileciteturn1file4
- Для *many-to-many* удобно транслировать CUD мостовой сущности в membership‑события.
- При больших списках — `limits` и/или `countOnlyWhenLarge` (свёртка в счётчики). fileciteturn1file3

### 6.2 Пресеты для коллекций (`collectionPresets`)

```yaml
collectionPresets:
  delta_verbose: { logDeltas: true,  showKeys: true,  showNames: true,  itemViewTemplate: "{name} (ID={key})" }
  delta_compact: { logDeltas: true,  showKeys: false, showNames: true,  itemViewTemplate: "{name}" }
  full_compact: { logDeltas: false, showKeys: false, showNames: true }
  count_only: { logDeltas: true,  showKeys: false, showNames: false, collapseToCounters: true }
```

---

## 7. Глобальные умолчания и пресеты

```yaml
referenceDefaults:
  showKey: true
  showName: true
  viewTemplate: "{name} (ID={key})"
  nameResolve: { stage: normalization, fallback: "{key}", maxLen: 256 }

collectionDefaults:
  logDeltas: true
  showKeys: true
  showNames: true
  itemViewTemplate: "{name} (ID={key})"
  limits: { addedMax: 200, removedMax: 200 }
```

Локальные настройки **перекрывают** пресет, пресет **перекрывает** defaults. fileciteturn1file1

---

## 8. Пример целостной политики (фрагмент)

```yaml
auditPolicy:
  version: 1.3
  mode: whitelist
  onCreate: eventOnly
  onUpdate: delta
  onDelete: eventOnly

  methodPresets: { ...см. §4 ... }
  referencePresets: { ...см. §5.2 ... }
  collectionPresets: { ...см. §6.2 ... }
  referenceDefaults: { ...см. §7 ... }
  collectionDefaults: { ...см. §7 ... }

  globalFieldExclusions: [ RowVersion, ConcurrencyToken ]

  entities:
    User:
      enabled: true
      onCreate: allFields
      fields:
        Password: { action: exclude }
        Email: { action: mask,    mask: { preset: email } }
        SSN: { action: hash,    hash: { preset: sha256_salted } }
        Card: { action: encrypt, encrypt: { preset: aes_gcm_default } }
        BirthDate:{ action:
          include, view: { format: date, pattern: "dd.MM.yyyy" } }
      references:
        DepartmentId:
          preset: fk_verbose
          nameSelector: "Department.Name"
      collections:
        Roles:
          preset: delta_verbose
          itemNameSelector: "Role.Name"
          limits: { addedMax: 50, removedMax: 50 }

    Order:
      enabled: true
      fields:
        TotalAmount: { action: include, view: { format: money, culture: "ru-RU" } }
        CreditCardCVV: { action: exclude }
      references:
        CustomerId: { preset: fk_key_only }
      collections:
        Items:
          preset: full_compact
          itemNameSelector: "Product.Name"
```

---

## 9. Шаблоны для UI и локализация/время

- **Update**: «Значение поля “\<Название\>” было изменено с “\<было\>” на “\<стало\>”. (\<дата‑время\>, \<ФИО\>)»
- **Create**: «Создана запись “\<Сущность\>”. (\<дата‑время\>, \<ФИО\>)»
- **Delete**: «Удалена запись “\<Сущность\>”. (\<дата‑время\>, \<ФИО\>)»
- **Коллекции**: «Добавлен элемент “\<Название\>”», «Удалён элемент “\<Название\>”».
- Язык — русский (локализация допускается); таймзона — локаль проекта (по умолчанию `Asia/Almaty`); в логе храним **UTC
  **. fileciteturn1file2

---

## 10. Массовые операции (без трекинга)

Для `ExecuteUpdate/ExecuteDelete/SQL` нет дельт по полям/коллекциям (ChangeTracker не видит изменений). Политика должна
фиксировать **факт операции**, **кто/когда**, **условие**, **количество затронутых строк**, опционально — **комментарий
**. fileciteturn1file8

---

## 11. Best practices и контроль объёма

- Не храните секреты в YAML — только **ссылки** на KMS/Secret Manager/ENV.
- Для больших коллекций — `limits`/`countOnlyWhenLarge`/свёртки. fileciteturn1file3
- Дорогие операции (резолв имён, форматирование) — на этапе **нормализации** (фоном). fileciteturn1file1
- Сохраняйте в raw **минимум** (особенно для PII): маска/шифр/хэш. fileciteturn1file2
- Соблюдайте независимость аудита: **никаких FK** на бизнес‑схему; сохраняйте текстовые лейблы/подписи.
  fileciteturn1file1

---

## 12. Чек‑лист приемки (в контексте политики)

- Для C/U/D создаются корректные события с `кто/когда/что` и дельтой полей.
- Enum/ссылки показываются человекочитаемо и **независимо** от текущего состояния БД.
- Чувствительные поля — маска/исключение/шифрование/хэш — **согласно политике**.
- UI получает **готовые сообщения** + метаданные; сырьё превращается в финальные записи в SLA. fileciteturn1file3

---

### Примечание по терминологии

- **Сырые логи** — фиксация факта изменений при сохранении.
- **Финальные логи** — дополнены «чел.» заголовками/значениями, готовы к показу.
- **Политика логирования** — правила, что и как логировать (вкл./искл., чувствительность и т. п.). fileciteturn1file3

