namespace ChangeLogMonitor.Interceptor.Services;

/// <summary>
///     Ambient-контекст для передачи метаданных аудита в raw SQL операциях.
///     Использует AsyncLocal для поддержки асинхронных операций.
/// </summary>
public sealed class AuditScope : IAuditScope
{
    private static readonly AsyncLocal<AuditScope?> CurrentScope = new();

    private readonly Dictionary<string, string> _hints = new();
    private readonly AuditScope? _parent;

    private AuditScope(AuditScope? parent = null)
    {
        _parent = parent;
    }

    /// <summary>
    ///     Получает текущий активный scope или null
    /// </summary>
    public static AuditScope? Current => CurrentScope.Value;

    /// <inheritdoc />
    public string? Reason { get; private set; }

    /// <inheritdoc />
    public string? TargetEntity { get; private set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Hints => _hints;

    /// <summary>
    ///     Создаёт новый scope и устанавливает его как текущий.
    ///     Поддерживает вложенность - при Dispose восстанавливает родительский scope.
    /// </summary>
    public static AuditScope Begin()
    {
        var scope = new AuditScope(CurrentScope.Value);
        CurrentScope.Value = scope;
        return scope;
    }

    /// <inheritdoc />
    public IAuditScope SetReason(string reason)
    {
        Reason = reason;
        return this;
    }

    /// <inheritdoc />
    public IAuditScope SetTargetEntity(string entity)
    {
        TargetEntity = entity;
        return this;
    }

    /// <inheritdoc />
    public IAuditScope AddHint(string key, string value)
    {
        _hints[key] = value;
        return this;
    }

    /// <summary>
    ///     Завершает scope и восстанавливает родительский
    /// </summary>
    public void Dispose()
    {
        CurrentScope.Value = _parent;
    }

    /// <summary>
    ///     Получает все hints включая унаследованные от родительских scopes
    /// </summary>
    public Dictionary<string, string> GetAllHints()
    {
        var allHints = new Dictionary<string, string>();

        // Собираем hints от родительских scopes
        var scope = this;
        var scopes = new Stack<AuditScope>();
        while (scope != null)
        {
            scopes.Push(scope);
            scope = scope._parent;
        }

        // Применяем в порядке от родителя к потомку (потомок перезаписывает)
        while (scopes.Count > 0)
        {
            var s = scopes.Pop();
            foreach (var hint in s._hints)
                allHints[hint.Key] = hint.Value;
        }

        return allHints;
    }

    /// <summary>
    ///     Получает reason с учётом родительских scopes (если не задан локально)
    /// </summary>
    public string? GetEffectiveReason()
    {
        var scope = this;
        while (scope != null)
        {
            if (!string.IsNullOrEmpty(scope.Reason))
                return scope.Reason;
            scope = scope._parent;
        }

        return null;
    }

    /// <summary>
    ///     Получает targetEntity с учётом родительских scopes (если не задан локально)
    /// </summary>
    public string? GetEffectiveTargetEntity()
    {
        var scope = this;
        while (scope != null)
        {
            if (!string.IsNullOrEmpty(scope.TargetEntity))
                return scope.TargetEntity;
            scope = scope._parent;
        }

        return null;
    }
}
