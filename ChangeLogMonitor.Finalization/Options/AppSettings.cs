using System.ComponentModel.DataAnnotations;

namespace ChangeLogMonitor.Finalization.Options;

public sealed class AppSettings
{
    public const string SectionName = "App";

    [Required] public KafkaSettings Kafka { get; init; } = new();

    [Required] public ClickHouseSettings ClickHouse { get; init; } = new();

    [Required] public HttpSettings Http { get; init; } = new();
}