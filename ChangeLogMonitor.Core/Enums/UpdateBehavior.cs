namespace ChangeLogMonitor.Core.Enums;

/// <summary>
/// Поведение при обновлении сущности
/// </summary>
public enum UpdateBehavior
{
    /// <summary>
    /// Только измененные поля (дельта)
    /// </summary>
    Delta,

    /// <summary>
    /// Полный снимок до и после
    /// </summary>
    FullSnapshot
}
