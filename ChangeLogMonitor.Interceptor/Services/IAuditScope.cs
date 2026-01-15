namespace ChangeLogMonitor.Interceptor.Services;

/// <summary>
///     Интерфейс для ambient-контекста аудита.
///     Позволяет передавать дополнительные метаданные для raw SQL операций.
/// </summary>
public interface IAuditScope : IDisposable
{
    /// <summary>
    ///     Причина/описание операции
    /// </summary>
    string? Reason { get; }

    /// <summary>
    ///     Целевая сущность/таблица
    /// </summary>
    string? TargetEntity { get; }

    /// <summary>
    ///     Дополнительные hints (key-value пары)
    /// </summary>
    IReadOnlyDictionary<string, string> Hints { get; }

    /// <summary>
    ///     Устанавливает причину операции
    /// </summary>
    IAuditScope SetReason(string reason);

    /// <summary>
    ///     Устанавливает целевую сущность
    /// </summary>
    IAuditScope SetTargetEntity(string entity);

    /// <summary>
    ///     Добавляет hint
    /// </summary>
    IAuditScope AddHint(string key, string value);
}
