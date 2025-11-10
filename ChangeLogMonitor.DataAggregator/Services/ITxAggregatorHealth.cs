using ChangeLogMonitor.DataAggregator.Processing;

namespace ChangeLogMonitor.DataAggregator.Services;

public interface ITxAggregatorHealth
{
    bool IsRunning { get; }
    bool IsReady { get; }
    string State { get; }
    TxAggregatorMetricsSnapshot GetSnapshot();
}
