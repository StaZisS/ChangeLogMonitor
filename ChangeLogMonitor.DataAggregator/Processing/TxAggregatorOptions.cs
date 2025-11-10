namespace ChangeLogMonitor.DataAggregator.Processing;

public sealed class TxAggregatorOptions
{
    public required int FlushIntervalMs { get; init; }
    public required int HardTtlMs { get; init; }
    public required int MaxEventsPerBucket { get; init; }
    public bool EmitPartialOnLimit { get; init; }
    public string? OutputTopic { get; init; }
}
