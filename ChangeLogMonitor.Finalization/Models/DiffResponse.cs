using System.Text.Json.Serialization;

namespace ChangeLogMonitor.Finalization.Models;

public sealed record DiffResponse(
    [property: JsonPropertyName("log_id")] long LogId,
    [property: JsonPropertyName("change_time")]
    DateTime ChangeTime,
    [property: JsonPropertyName("table_name")]
    string TableName,
    [property: JsonPropertyName("operation")]
    int Operation,
    [property: JsonPropertyName("entity_id")]
    string EntityId,
    [property: JsonPropertyName("tx_id")] string TransactionId,
    [property: JsonPropertyName("user_id")]
    string UserId,
    [property: JsonPropertyName("payload")]
    string Payload)
{
    public static DiffResponse FromRecord(AuditLogRecord record)
    {
        return new DiffResponse(
            record.LogId,
            record.ChangeTimeUtc,
            record.TableName,
            record.OperationCode,
            record.EntityId,
            record.TxId,
            record.UserId,
            record.Payload);
    }
}