namespace ChangeLogMonitor.Embedded.Options;

/// <summary>
/// Options for configuring ChangeLogMonitor in embedded mode.
/// </summary>
public class EmbeddedChangeLogOptions
{
    /// <summary>
    /// Path to the changelog configuration YAML file.
    /// Default: "changelog-config.yaml"
    /// </summary>
    public string ConfigFilePath { get; set; } = "changelog-config.yaml";

    /// <summary>
    /// Enable the EF Core interceptor to capture changes.
    /// Default: true
    /// </summary>
    public bool EnableInterceptor { get; set; } = true;

    /// <summary>
    /// Enable API endpoints (/healthz, /diffs, /debug).
    /// Default: true
    /// </summary>
    public bool EnableApi { get; set; } = true;

    /// <summary>
    /// Base path for API endpoints.
    /// Default: "/changelog"
    /// </summary>
    public string ApiBasePath { get; set; } = "/changelog";

    /// <summary>
    /// Finalization options (Kafka, ClickHouse).
    /// Only used if Finalization is enabled.
    /// </summary>
    public FinalizationOptions? Finalization { get; set; }
}

/// <summary>
/// Options for the Finalization service (Kafka consumer, ClickHouse storage).
/// </summary>
public class FinalizationOptions
{
    /// <summary>
    /// Enable Finalization service.
    /// When enabled, aggregates are consumed from Kafka and stored in ClickHouse.
    /// Default: false (embedded mode typically doesn't need this)
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Kafka bootstrap servers.
    /// </summary>
    public string? KafkaBootstrapServers { get; set; }

    /// <summary>
    /// Kafka input topic for aggregates.
    /// </summary>
    public string? KafkaInputTopic { get; set; }

    /// <summary>
    /// Kafka consumer group ID.
    /// </summary>
    public string? KafkaGroupId { get; set; }

    /// <summary>
    /// ClickHouse connection string.
    /// </summary>
    public string? ClickHouseConnectionString { get; set; }
}
