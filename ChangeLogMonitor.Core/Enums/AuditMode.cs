namespace ChangeLogMonitor.Core.Enums;

/// <summary>
/// Режим работы политики аудита (whitelist/blacklist)
/// </summary>
public enum AuditMode
{
    /// <summary>
    /// Белый список - логируются только указанные сущности
    /// </summary>
    Whitelist,

    /// <summary>
    /// Черный список - логируются все, кроме указанных
    /// </summary>
    Blacklist
}
