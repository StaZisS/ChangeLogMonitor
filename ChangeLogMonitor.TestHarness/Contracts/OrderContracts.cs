using ChangeLogMonitor.TestHarness.Domain;

namespace ChangeLogMonitor.TestHarness.Contracts;

public sealed record CreateOrderRequest(string Description, decimal Amount, OrderStatus Status = OrderStatus.New);

public sealed record UpdateOrderRequest(string Description, decimal Amount, OrderStatus? Status = null);

public sealed record ChangeOrderStatusRequest(OrderStatus Status);

public sealed record OrderResponse(
    Guid Id,
    string Description,
    decimal Amount,
    OrderStatus Status,
    string StatusLabel,
    DateTimeOffset CreatedAt,
    Guid UserId);