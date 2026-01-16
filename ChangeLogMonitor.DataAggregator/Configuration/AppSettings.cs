using System.ComponentModel.DataAnnotations;

namespace ChangeLogMonitor.DataAggregator.Configuration;

public sealed class AppSettings
{
    public const string SectionName = "Aggregator";

    [Required] public KafkaSettings Kafka { get; init; } = new();

    [Required] public ProcessingSettings Processing { get; init; } = new();
}