using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("LeaveApprovals")]
[Index(nameof(RequestId), nameof(ApproverRole), IsUnique = true)]
[Index(nameof(ApproverEmployeeId))]
public sealed class LeaveApproval
{
    [Key]
    public int ApprovalId { get; set; }

    public int RequestId { get; set; }

    [ForeignKey(nameof(RequestId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public LeaveRequest Request { get; set; } = null!;

    public LeaveApproverRole ApproverRole { get; set; }

    public int? ApproverEmployeeId { get; set; }

    [ForeignKey(nameof(ApproverEmployeeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee? ApproverEmployee { get; set; }

    public LeaveApprovalDecision Decision { get; set; } = LeaveApprovalDecision.Pending;

    public DateTimeOffset? DecisionDate { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
