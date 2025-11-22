namespace ChangeLogMonitor.TestHarness.Contracts;

public sealed record CreateOrderRequest(string Description, decimal Amount);

public sealed record UpdateOrderRequest(string Description, decimal Amount);

public sealed record OrderResponse(
    Guid Id,
    string Description,
    decimal Amount,
    DateTimeOffset CreatedAt,
    Guid UserId);