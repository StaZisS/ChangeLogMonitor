using ChangeLogMonitor.Core.Enums;

namespace ChangeLogMonitor.Finalization.Models;

public sealed record AuditLogRecord(
    long LogId,
    DateTime ChangeTimeUtc,
    string UserId,
    string UserName,
    string TableName,
    OperationCode Operation,
    string EntityId,
    string TxId,
    string Payload);