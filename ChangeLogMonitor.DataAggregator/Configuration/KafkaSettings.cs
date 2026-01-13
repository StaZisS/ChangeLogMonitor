using System.ComponentModel.DataAnnotations;

namespace ChangeLogMonitor.DataAggregator.Configuration;

public sealed class KafkaSettings
{
    [Required] public string BootstrapServers { get; init; } = "localhost:9092";

    [MinLength(1)] public string[] InputCdcTopics { get; init; } = Array.Empty<string>();

    [Required] public string MetadataTopic { get; init; } = "app.transaction_meta";

    [Required] public string OutputTopic { get; init; } = "aggregates.by_tx";

    [Required] public string RepartitionTopic { get; init; } = "agg.changes.by_tx";

    [Required] public string ApplicationId { get; init; } = "changemonitor-data-aggregator";

    public string StateDir { get; init; } = Path.Combine(AppContext.BaseDirectory, "state");

    public string? DlqTopic { get; init; }

    public bool EnableAutoTopicCreation { get; init; } = false;

    public int DefaultNumPartitions { get; init; } = 1;
}