using ChangeLogMonitor.TestHarness.Domain;

namespace ChangeLogMonitor.TestHarness.Contracts;

/// <summary>
///     Запрос на массовое обновление статуса заказов
/// </summary>
public record BatchUpdateStatusRequest(
    OrderStatus FromStatus,
    OrderStatus ToStatus);

/// <summary>
///     Запрос на массовое удаление заказов по статусу
/// </summary>
public record BatchDeleteByStatusRequest(
    OrderStatus Status);

/// <summary>
///     Запрос на raw SQL обновление
/// </summary>
public record RawUpdateRequest(
    decimal MinAmount,
    decimal MaxAmount,
    OrderStatus NewStatus,
    string? Reason = null);

/// <summary>
///     Результат массовой операции
/// </summary>
public record BulkOperationResult(
    int AffectedCount,
    string Operation,
    string? Reason = null);
