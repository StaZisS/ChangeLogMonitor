using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChangeLogMonitor.Interceptor.Models;

[Table("audit_log")]
public class AuditLog
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [Required]
    [Column("transaction_id")]
    [MaxLength(255)]
    public string TransactionId { get; set; } = null!;

    [Required]
    [Column("payload")]
    public byte[] Payload { get; set; } = null!;
}