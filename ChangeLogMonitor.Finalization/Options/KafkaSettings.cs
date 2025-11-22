using System.ComponentModel.DataAnnotations;

namespace ChangeLogMonitor.Finalization.Options;

public sealed class KafkaSettings
{
    [Required] public string BootstrapServers { get; init; } = "localhost:9092";

    [Required] public string InputTopic { get; init; } = "aggregates.by_tx";

    [Required] public string GroupId { get; init; } = "changemonitor-finalizer";

    public bool EnableAutoCreateTopics { get; init; }
}