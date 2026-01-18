using ChangeLogMonitor.DataAggregator.Configuration;
using ChangeLogMonitor.DataAggregator.Processing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Streamiz.Kafka.Net;
using StreamState = Streamiz.Kafka.Net.KafkaStream.State;

namespace ChangeLogMonitor.DataAggregator.Services;

public sealed class TxAggregatorHostedService : BackgroundService, ITxAggregatorHealth
{
    private readonly string _applicationId;
    private readonly ILogger<TxAggregatorHostedService> _logger;
    private readonly TxAggregatorMetrics _metrics;
    private readonly TxAggregatorTopologyFactory _topologyFactory;
    private volatile bool _isReady;
    private volatile bool _isRunning;

    private KafkaStream? _stream;

    public TxAggregatorHostedService(
        TxAggregatorTopologyFactory topologyFactory,
        IOptions<AppSettings> settings,
        TxAggregatorMetrics metrics,
        ILogger<TxAggregatorHostedService> logger)
    {
        _topologyFactory = topologyFactory;
        _logger = logger;
        _metrics = metrics;
        _applicationId = settings.Value.Kafka.ApplicationId;
    }

    public bool IsRunning => _isRunning;
    public bool IsReady => _isReady;
    public string State { get; private set; } = StreamState.CREATED.ToString();

    public TxAggregatorMetricsSnapshot GetSnapshot()
    {
        return _metrics.GetSnapshot();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var (topology, config) = _topologyFactory.Build();
        var stream = new KafkaStream(topology, config);
        _stream = stream;
        stream.StateChanged += OnStateChanged;

        _logger.LogInformation("Starting Kafka Streams application {AppId}", _applicationId);

        try
        {
            _isRunning = true;
            await stream.StartAsync(stoppingToken);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Kafka stream {AppId} cancellation requested", _applicationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka stream {AppId} terminated unexpectedly", _applicationId);
            throw;
        }
        finally
        {
            stream.StateChanged -= OnStateChanged;
            try
            {
                stream.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            _stream = null;
            _isRunning = false;
            _isReady = false;
            State = StreamState.NOT_RUNNING.ToString();
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Kafka Streams application {AppId}", _applicationId);
        _isReady = false;
        return base.StopAsync(cancellationToken);
    }

    private void OnStateChanged(StreamState previous, StreamState current)
    {
        State = current.ToString();
        _isReady = ReferenceEquals(current, StreamState.RUNNING);
        _logger.LogInformation("Kafka stream state changed {OldState} -> {NewState}", previous, current);
    }
}