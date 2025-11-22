using System.ComponentModel.DataAnnotations;

namespace ChangeLogMonitor.Finalization.Options;

public sealed class ClickHouseSettings
{
    [Required]
    public string ConnectionString { get; init; } =
        "Host=localhost;Port=9000;Database=default;User=default;Password=";

    [Required] public string TableName { get; init; } = "audit_log";

    public bool EnsureSchema { get; init; } = true;
}