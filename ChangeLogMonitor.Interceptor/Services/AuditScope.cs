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
    
    public static AuditScope? Current => CurrentScope.Value;

    public string? Reason { get; private set; }
    
    public string? TargetEntity { get; private set; }
    
    public IReadOnlyDictionary<string, string> Hints => _hints;
    
    public static AuditScope Begin()
    {
        var scope = new AuditScope(CurrentScope.Value);
        CurrentScope.Value = scope;
        return scope;
    }
    
    public IAuditScope SetReason(string reason)
    {
        Reason = reason;
        return this;
    }
    
    public IAuditScope SetTargetEntity(string entity)
    {
        TargetEntity = entity;
        return this;
    }
    
    public IAuditScope AddHint(string key, string value)
    {
        _hints[key] = value;
        return this;
    }

    public void Dispose()
    {
        CurrentScope.Value = _parent;
    }

    public Dictionary<string, string> GetAllHints()
    {
        var allHints = new Dictionary<string, string>();
        
        var scope = this;
        var scopes = new Stack<AuditScope>();
        while (scope != null)
        {
            scopes.Push(scope);
            scope = scope._parent;
        }
        
        while (scopes.Count > 0)
        {
            var s = scopes.Pop();
            foreach (var hint in s._hints)
                allHints[hint.Key] = hint.Value;
        }

        return allHints;
    }
    
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
