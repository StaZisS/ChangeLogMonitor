# ChangeLogMonitor

Приложение для аудита изменений данных в базе данных на основе Debezium CDC.

## Описание проекта

ChangeLogMonitor - это модульная система аудита, которая позволяет отслеживать и визуализировать все изменения данных в базе данных. Система построена на основе Change Data Capture (CDC) с использованием Debezium и может работать как встроенно в основное приложение, так и как отдельный сервис.

## Архитектура

Проект разделен на модули для обеспечения гибкости развертывания:

### Основные модули

- **ChangeLogMonitor.Core** - Базовые интерфейсы, модели и абстракции
- **ChangeLogMonitor.Interceptor** - EF Core интерцептор для перехвата изменений в транзакциях
- **ChangeLogMonitor.Configuration** - Работа с YAML конфигурацией (настройка логирования, маскирования полей)
- **ChangeLogMonitor.DataAggregator** - Агрегация данных из Kafka/Debezium и соединение с метаданными
- **ChangeLogMonitor.UI** - Razor Pages UI для отображения журнала изменений
- **ChangeLogMonitor.Api** - REST API для работы с данными журнала

### Режимы развертывания

- **ChangeLogMonitor.Standalone** - Отдельное приложение для развертывания системы аудита как standalone сервиса
- **ChangeLogMonitor.Embedded** - Библиотека для встраивания функциональности аудита в существующее приложение

## Принцип работы

1. **Захват изменений**: При завершении транзакции EF Core интерцептор записывает изменения в:
   - Таблицы с данными изменений
   - Таблицу с метаданными (кто, когда, контекст операции)

2. **CDC через Debezium**: Debezium отслеживает изменения в базе данных и отправляет события в Kafka

3. **Агрегация данных**: DataAggregator модуль:
   - Получает CDC события из Kafka
   - Соединяет данные изменений с метаданными
   - Подготавливает полную информацию для отображения

4. **Визуализация**: UI показывает пользователям:
   - Журнал всех изменений
   - Детали изменений (до/после)
   - Метаданные (кто изменил, когда, контекст)
   - Возможности фильтрации и поиска

## Конфигурация

Конфигурация разделена на два файла:

### 1. changelog-config.yaml
Настройки аудита (что логировать):
- Какие таблицы/сущности логировать
- Какие поля маскировать (для защиты чувствительных данных)
- Правила фильтрации
- Настройки хранения данных
- Метаданные для сбора

**Пример:** скопируйте `changelog-config.example.yaml` в `changelog-config.yaml` и настройте под ваши нужды.

### 2. appsettings.json
Инфраструктурные настройки (как подключаться):
- Строки подключения к базам данных (основная и для аудита)
- Настройки Kafka (bootstrap servers, топики, consumer group)
- Настройки Debezium (connector, database credentials)
- Настройки хранилища аудита
- Уровни логирования .NET

**Пример:** скопируйте `appsettings.example.json` в `appsettings.json` и настройте подключения.

## Требования

- .NET 8.0
- Entity Framework Core
- Kafka
- Debezium
- PostgreSQL/SQL Server/MySQL (зависит от вашей БД)

## Структура проекта

```
ChangeLogMonitor/
├── ChangeLogMonitor.Core/              # Базовые абстракции
├── ChangeLogMonitor.Interceptor/       # EF Core интерцептор
├── ChangeLogMonitor.Configuration/     # YAML конфигурация
├── ChangeLogMonitor.DataAggregator/    # Kafka consumer и агрегация
├── ChangeLogMonitor.UI/                # Razor Pages UI
├── ChangeLogMonitor.Api/               # REST API
├── ChangeLogMonitor.Standalone/        # Standalone приложение
├── ChangeLogMonitor.Embedded/          # Библиотека для встраивания
├── ChangeLogMonitor.sln                # Solution файл
├── README.md                           # Документация
├── changelog-config.example.yaml       # Пример конфигурации аудита
└── appsettings.example.json            # Пример инфраструктурных настроек
```

## Следующие шаги

1. Реализовать модели данных в Core
2. Настроить интерцептор EF Core
3. Создать провайдер YAML конфигурации
4. Реализовать Kafka consumer для Debezium
5. Создать UI для отображения журнала
6. Настроить API endpoints
7. Подготовить примеры конфигурации
