namespace ChangeLogMonitor.TestHarness.Contracts;

public sealed record CreateTagRequest(string Name, string? Color);

public sealed record UpdateTagRequest(string Name, string? Color);

public sealed record TagResponse(Guid Id, string Name, string? Color);

public sealed record AddTagToOrderRequest(Guid TagId);

public sealed record ReassignOrderRequest(Guid NewUserId);
