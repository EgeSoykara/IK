using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("AuditLogs")]
[Index(nameof(ActionDate))]
[Index(nameof(ActionType))]
[Index(nameof(EntityName), nameof(EntityId))]
public sealed class AuditLog
{
    [Key]
    public long AuditLogId { get; set; }

    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    public AuditActionType ActionType { get; set; }

    [Required]
    [MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string EntityId { get; set; } = string.Empty;

    public DateTimeOffset ActionDate { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(1000)]
    public string? Details { get; set; }
}
