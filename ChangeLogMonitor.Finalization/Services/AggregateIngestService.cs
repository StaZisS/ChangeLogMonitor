using ChangeLogMonitor.Finalization.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace ChangeLogMonitor.Finalization.Services;

internal sealed class AggregateIngestService : BackgroundService
{
    private readonly IAggregateFlattener _flattener;
    private readonly KafkaSettings _kafkaSettings;
    private readonly ILogger<AggregateIngestService> _logger;
    private readonly IAuditLogRepository _repository;

    public AggregateIngestService(
        IOptions<AppSettings> options,
        IAggregateFlattener flattener,
        IAuditLogRepository repository,
        ILogger<AggregateIngestService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _kafkaSettings = options.Value.Kafka;
        _flattener = flattener;
        _repository = repository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = _kafkaSettings.GroupId,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ClientId = $"finalizer-{Environment.MachineName}".ToLowerInvariant(),
            AllowAutoCreateTopics = _kafkaSettings.EnableAutoCreateTopics
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) =>
            {
                if (!stoppingToken.IsCancellationRequested) _logger.LogError("Kafka error: {Reason}", e.Reason);
            })
            .Build();

        consumer.Subscribe(_kafkaSettings.InputTopic);
        _logger.LogInformation("Finalizer listening on topic {Topic}", _kafkaSettings.InputTopic);

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message == null) continue;

                var rows = _flattener.Flatten(result.Message.Value, stoppingToken);
                if (rows.Count > 0)
                {
                    await _repository.InsertAsync(rows, stoppingToken);
                    _logger.LogDebug("Stored {Count} audit rows tx={TxId}", rows.Count, rows.First().TxId);
                }

                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogWarning(ex, "Consume error at {TopicPartitionOffset}",
                    ex.ConsumerRecord?.TopicPartitionOffset);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest aggregate batch");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }

        try
        {
            consumer.Close();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Kafka consumer close failure (ignored)");
        }
    }
}