using System;
using System.Linq;
using ChangeLogMonitor.DataAggregator.Configuration;
using ChangeLogMonitor.DataAggregator.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Streamiz.Kafka.Net;
using Streamiz.Kafka.Net.Processors.Public;
using Streamiz.Kafka.Net.SerDes;
using Streamiz.Kafka.Net.State;
using Streamiz.Kafka.Net.Stream;
using Streamiz.Kafka.Net.Table;
using Streamiz.Kafka.Net.Crosscutting;

namespace ChangeLogMonitor.DataAggregator.Services;

public sealed class TxAggregatorTopologyFactory
{
    private readonly AppSettings _settings;
    private readonly ILogger<TxAggregatorTopologyFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TxAggregatorMetrics _metrics;

    public TxAggregatorTopologyFactory(
        IOptions<AppSettings> options,
        ILogger<TxAggregatorTopologyFactory> logger,
        ILoggerFactory loggerFactory,
        TxAggregatorMetrics metrics)
    {
        _settings = options.Value;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _metrics = metrics;
    }

    public (Topology Topology, StreamConfig<StringSerDes, StringSerDes> Config) Build()
    {
        var builder = new StreamBuilder();
        var keySerde = new StringSerDes();
        var valueSerde = new StringSerDes();

        var cdcStream = BuildCdcSource(builder, keySerde, valueSerde);
        BuildMetadataTable(builder, keySerde, valueSerde);
        RegisterStateStore(builder, keySerde, valueSerde);

        var transformerOptions = new TxAggregatorOptions
        {
            FlushIntervalMs = _settings.Processing.FlushIntervalMs,
            HardTtlMs = _settings.Processing.HardTtlMs,
            MaxEventsPerBucket = _settings.Processing.MaxEventsPerBucket,
            EmitPartialOnLimit = _settings.Processing.EmitPartialOnLimit
        };

        TxAggregatorTransformerDependencies.Configure(transformerOptions, _loggerFactory, _metrics);

        var transformerSupplier = TransformerBuilder
            .New<string, string, string, string>()
            .Transformer<TxAggregatorTransformer>()
            .Build();

        var rekeyed = cdcStream
            .SelectKey((_, value, _) => NormalizeTxId(TxCdcParser.ExtractTxId(value)), "tx-id-select");

        var branches = rekeyed.Branch(
            "tx-id-branch",
            (key, _, _) => !string.IsNullOrWhiteSpace(key),
            (_, _, _) => true);

        var valid = branches[0];
        var invalid = branches[1];

        HandleInvalidTransactions(invalid, keySerde, valueSerde);

        var repartitionedConfig = Repartitioned<string, string>
            .As(_settings.Kafka.RepartitionTopic)
            .WithKeySerdes(keySerde)
            .WithValueSerdes(valueSerde);

        var repartitioned = valid.Repartition(repartitionedConfig);

        var aggregated = repartitioned.Transform(
            transformerSupplier,
            "tx-aggregator",
            StoreNames.TxStateStore,
            StoreNames.TxMetaStore);

        aggregated
            .Filter((_, value, _) => !string.IsNullOrWhiteSpace(value), "aggregate-filter")
            .To(_settings.Kafka.OutputTopic, keySerde, valueSerde, "tx-aggregates");

        var topology = builder.Build();
        var config = BuildStreamConfig(keySerde, valueSerde);
        return (topology, config);
    }

    private IKStream<string, string> BuildCdcSource(
        StreamBuilder builder,
        StringSerDes keySerde,
        StringSerDes valueSerde)
    {
        IKStream<string, string>? stream = null;
        foreach (var topic in _settings.Kafka.InputCdcTopics.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var nodeName = $"cdc-{topic.Replace('.', '-')}";
            var topicStream = builder.Stream<string, string>(topic, keySerde, valueSerde, nodeName);
            stream = stream == null
                ? topicStream
                : stream.Merge(topicStream, $"merge-{nodeName}");
        }

        return stream ?? throw new InvalidOperationException("At least one CDC topic must be configured.");
    }

    private void BuildMetadataTable(StreamBuilder builder, StringSerDes keySerde, StringSerDes valueSerde)
    {
        var materialized = Materialized<string, string, IKeyValueStore<Bytes, byte[]>>
            .Create(Stores.PersistentKeyValueStore(StoreNames.TxMetaStore))
            .WithKeySerdes(keySerde)
            .WithValueSerdes(valueSerde)
            .WithLoggingDisabled()
            .WithCachingDisabled()
            .WithName(StoreNames.TxMetaStore);

        builder.GlobalTable<string, string>(
            _settings.Kafka.MetadataTopic,
            keySerde,
            valueSerde,
            materialized);
    }

    private void RegisterStateStore(StreamBuilder builder, StringSerDes keySerde, StringSerDes valueSerde)
    {
        var storeBuilder = Stores.KeyValueStoreBuilder(
            Stores.PersistentKeyValueStore(StoreNames.TxStateStore),
            keySerde,
            valueSerde);

        builder.AddStateStore(storeBuilder);
    }

    private void HandleInvalidTransactions(IKStream<string, string> invalidStream, StringSerDes keySerde, StringSerDes valueSerde)
    {
        var logLevel = _settings.Processing.RejectWithoutTxId ? LogLevel.Warning : LogLevel.Debug;

        invalidStream = invalidStream.Peek((_, _, ctx) =>
        {
            _metrics.MarkMissingTxId();
            _logger.Log(logLevel,
                "Dropping CDC event without tx_id from {Topic}[{Partition}]@{Offset}",
                ctx.Topic,
                ctx.Partition,
                ctx.Offset);
        }, "missing-tx-log");

        if (_settings.Processing.RejectWithoutTxId && !string.IsNullOrWhiteSpace(_settings.Kafka.DlqTopic))
        {
            invalidStream.To(_settings.Kafka.DlqTopic, keySerde, valueSerde, "missing-tx-dlq");
        }
    }

    private StreamConfig<StringSerDes, StringSerDes> BuildStreamConfig(StringSerDes keySerde, StringSerDes valueSerde)
    {
        var config = new StreamConfig<StringSerDes, StringSerDes>
        {
            ApplicationId = _settings.Kafka.ApplicationId,
            BootstrapServers = _settings.Kafka.BootstrapServers,
            Guarantee = ProcessingGuarantee.EXACTLY_ONCE,
            StateDir = _settings.Kafka.StateDir,
            AllowAutoCreateTopics = _settings.Kafka.EnableAutoTopicCreation,
            CommitIntervalMs = _settings.Processing.FlushIntervalMs,
            NumStreamThreads = Math.Max(1, Environment.ProcessorCount / 2),
            ClientId = $"{_settings.Kafka.ApplicationId}-{Environment.MachineName}".ToLowerInvariant(),
            TransactionTimeout = TimeSpan.FromSeconds(60),
            Logger = _loggerFactory
        };

        config.DefaultKeySerDes = keySerde;
        config.DefaultValueSerDes = valueSerde;
        config.EnableIdempotence = true;
        config.ReplicationFactor = 1;
        config.RocksDbConfigHandler = (_, rocksDbOptions) =>
        {
            var parallelism = Math.Max(1, Environment.ProcessorCount / 2);
            rocksDbOptions.IncreaseParallelism(parallelism);
        };

        return config;
    }

    private static string NormalizeTxId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
