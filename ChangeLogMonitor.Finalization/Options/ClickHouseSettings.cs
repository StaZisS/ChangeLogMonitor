using System.ComponentModel.DataAnnotations;

namespace ChangeLogMonitor.Finalization.Options;

public sealed class ClickHouseSettings
{
    [Required]
    public string ConnectionString { get; init; } =
        "Host=localhost;Port=8123;Database=default;User=default;Password=123456";

    [Required] public string TableName { get; init; } = "audit_log";

    public bool EnsureSchema { get; init; } = true;
}