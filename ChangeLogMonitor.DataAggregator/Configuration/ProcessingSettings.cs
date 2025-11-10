using System.ComponentModel.DataAnnotations;

namespace ChangeLogMonitor.DataAggregator.Configuration;

public sealed class ProcessingSettings
{
    [Range(200, 60000)]
    public int FlushIntervalMs { get; init; } = 800;

    [Range(500, 60000)]
    public int HardTtlMs { get; init; } = 2000;

    [Range(10, 5000)]
    public int MaxEventsPerBucket { get; init; } = 1000;

    public bool RejectWithoutTxId { get; init; } = true;

    public bool EmitPartialOnLimit { get; init; } = true;
}
