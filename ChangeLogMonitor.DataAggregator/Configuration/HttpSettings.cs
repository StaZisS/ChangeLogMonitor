using System.ComponentModel.DataAnnotations;

namespace ChangeLogMonitor.DataAggregator.Configuration;

public sealed class HttpSettings
{
    [Range(80, 65535)] public int Port { get; init; } = 8080;

    public string Url => $"http://0.0.0.0:{Port}";
}