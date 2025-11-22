using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ChangeLogMonitor.Finalization.Models;
using ChangeLogMonitor.Finalization.Options;
using ClickHouse.Client.ADO;
using Microsoft.Extensions.Options;

namespace ChangeLogMonitor.Finalization.Services;

internal sealed class ClickHouseAuditLogRepository : IAuditLogRepository
{
    private const string CreateTableTemplate = @"
CREATE TABLE IF NOT EXISTS {0} (
    log_id UInt64,
    change_time DateTime,
    user_id String,
    table_name LowCardinality(String),
    operation UInt8,
    entity_id String,
    tx_id String,
    payload String
) ENGINE = MergeTree()
PARTITION BY toYYYYMM(change_time)
ORDER BY (log_id, change_time, table_name, user_id)
SETTINGS index_granularity = 8192;";

    private readonly string _connectionString;
    private readonly ILogger<ClickHouseAuditLogRepository> _logger;
    private readonly string _quotedTableName;
    private readonly ClickHouseSettings _settings;
    private readonly string _tableName;

    public ClickHouseAuditLogRepository(IOptions<AppSettings> options, ILogger<ClickHouseAuditLogRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _settings = options.Value.ClickHouse;
        _logger = logger;
        _tableName = ValidateTableName(_settings.TableName);
        _quotedTableName = Quote(_tableName);
        _connectionString = _settings.ConnectionString;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (!_settings.EnsureSchema) return;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = string.Format(CultureInfo.InvariantCulture, CreateTableTemplate, _quotedTableName);
        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Ensured ClickHouse table {Table}", _tableName);
    }

    public async Task InsertAsync(IReadOnlyCollection<AuditLogRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0) return;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var builder = new StringBuilder();
        var index = 0;

        foreach (var record in records)
        {
            if (index == 0)
                builder.Append(
                    $"INSERT INTO {_quotedTableName} (log_id, change_time, user_id, table_name, operation, entity_id, tx_id, payload) VALUES ");
            else
                builder.Append(',');

            builder.Append(
                $"(@id{index}, @ct{index}, @uid{index}, @tbl{index}, @op{index}, @eid{index}, @tx{index}, @payload{index})");

            command.Parameters.Add(CreateParameter(command, $"id{index}", GenerateLogId(record)));
            command.Parameters.Add(CreateParameter(command, $"ct{index}",
                DateTime.SpecifyKind(record.ChangeTimeUtc, DateTimeKind.Utc)));
            command.Parameters.Add(CreateParameter(command, $"uid{index}", record.UserId ?? string.Empty));
            command.Parameters.Add(CreateParameter(command, $"tbl{index}", record.TableName ?? string.Empty));
            command.Parameters.Add(CreateParameter(command, $"op{index}", record.OperationCode));
            command.Parameters.Add(CreateParameter(command, $"eid{index}", record.EntityId ?? string.Empty));
            command.Parameters.Add(CreateParameter(command, $"tx{index}", record.TxId ?? string.Empty));
            command.Parameters.Add(CreateParameter(command, $"payload{index}", record.Payload ?? string.Empty));

            index++;
        }

        command.CommandText = builder.ToString();
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogRecord>> GetByEntityAsync(
        string tableName,
        string entityId,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedTable = tableName?.Trim() ?? string.Empty;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            $"SELECT log_id, change_time, user_id, table_name, operation, entity_id, tx_id, payload FROM {_quotedTableName} " +
            "WHERE table_name = @table AND entity_id = @entity " +
            "ORDER BY log_id DESC LIMIT @limit";

        command.Parameters.Add(CreateParameter(command, "table", normalizedTable));
        command.Parameters.Add(CreateParameter(command, "entity", entityId));
        command.Parameters.Add(CreateParameter(command, "limit", limit));

        return await ReadAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogRecord>> GetByTransactionAsync(
        string transactionId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            $"SELECT log_id, change_time, user_id, table_name, operation, entity_id, tx_id, payload FROM {_quotedTableName} " +
            "WHERE tx_id = @tx " +
            "ORDER BY log_id DESC LIMIT @limit";

        command.Parameters.Add(CreateParameter(command, "tx", transactionId));
        command.Parameters.Add(CreateParameter(command, "limit", limit));

        return await ReadAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<AuditLogRecord>> ReadAsync(
        ClickHouseCommand command,
        CancellationToken cancellationToken)
    {
        var result = new List<AuditLogRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var logId = reader.GetFieldValue<ulong>(0);
            var changeTime = reader.GetDateTime(1);
            var userId = reader.GetFieldValue<string>(2);
            var tableName = reader.GetFieldValue<string>(3);
            var operation = Convert.ToByte(reader.GetValue(4), CultureInfo.InvariantCulture);
            var entityId = reader.GetFieldValue<string>(5);
            var txId = reader.GetFieldValue<string>(6);
            var payload = reader.GetFieldValue<string>(7);

            result.Add(new AuditLogRecord((long)logId, changeTime, userId, tableName, operation, entityId, txId,
                payload));
        }

        return result;
    }

    private async Task<ClickHouseConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new ClickHouseConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string ValidateTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("ClickHouse:TableName must be provided", nameof(tableName));

        if (!Regex.IsMatch(tableName, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
            throw new ArgumentException("ClickHouse:TableName must contain only letters, digits or underscore.",
                nameof(tableName));

        return tableName;
    }

    private static string Quote(string identifier)
    {
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }

    private static DbParameter CreateParameter(ClickHouseCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        return parameter;
    }

    private static ulong GenerateLogId(AuditLogRecord record)
    {
        // грубый монотонный surrogate: combine change time ms + hash of tx/entity
        var time = new DateTimeOffset(record.ChangeTimeUtc).ToUnixTimeMilliseconds();
        var suffix = (ulong)string.GetHashCode($"{record.TxId}:{record.EntityId}:{record.OperationCode}");
        return ((ulong)time << 16) ^ suffix;
    }
}