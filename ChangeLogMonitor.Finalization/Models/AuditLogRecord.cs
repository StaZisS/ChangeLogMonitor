namespace ChangeLogMonitor.Finalization.Models;

public sealed record AuditLogRecord(
    long LogId,
    DateTime ChangeTimeUtc,
    string UserId,
    string TableName,
    byte OperationCode,
    string EntityId,
    string TxId,
    string Payload);