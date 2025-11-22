using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChangeLogMonitor.Interceptor.Models;

/// <summary>
///     Модель записи аудита, соответствующая таблице audit_log
/// </summary>
[Table("audit_log")]
public class AuditLog
{
    /// <summary>
    ///     Уникальный идентификатор записи
    /// </summary>
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    ///     Момент фиксации изменения (UTC)
    /// </summary>
    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Время обработки записи (NULL когда запись не обработана)
    /// </summary>
    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    ///     Идентификатор транзакции (общий для всех изменений в рамках одной транзакции)
    /// </summary>
    [Required]
    [Column("transaction_id")]
    [MaxLength(255)]
    public string TransactionId { get; set; } = null!;

    /// <summary>
    ///     Сериализованный AuditMetaEnvelope (protobuf)
    /// </summary>
    [Required]
    [Column("payload")]
    public byte[] Payload { get; set; } = null!;
}