using System.ComponentModel.DataAnnotations;

namespace ChangeLogMonitor.Finalization.Options;

public sealed class HttpSettings
{
    [Range(1, 65535)] public int Port { get; init; } = 8081;
}