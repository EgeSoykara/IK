using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("LeaveCancellationApprovals")]
[Index(nameof(CancellationRequestId), nameof(ApproverRole), IsUnique = true)]
public sealed class LeaveCancellationApproval
{
    [Key] public int CancellationApprovalId { get; set; }
    public int CancellationRequestId { get; set; }

    [ForeignKey(nameof(CancellationRequestId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public LeaveCancellationRequest CancellationRequest { get; set; } = null!;

    public LeaveApproverRole ApproverRole { get; set; }
    public int? ApproverEmployeeId { get; set; }

    [ForeignKey(nameof(ApproverEmployeeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee? ApproverEmployee { get; set; }

    public LeaveApprovalDecision Decision { get; set; } = LeaveApprovalDecision.Pending;
    public DateTimeOffset? DecisionDate { get; set; }
    [MaxLength(500)] public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
