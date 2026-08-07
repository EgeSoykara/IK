using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("LeaveCancellationRequests")]
[Index(nameof(LeaveRequestId), nameof(CurrentStatus))]
public sealed class LeaveCancellationRequest
{
    [Key] public int CancellationRequestId { get; set; }
    public int LeaveRequestId { get; set; }

    [ForeignKey(nameof(LeaveRequestId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LeaveRequest LeaveRequest { get; set; } = null!;

    [Column(TypeName = "date")] public DateTime CancellationStartDate { get; set; }
    [Column(TypeName = "date")] public DateTime CancellationEndDate { get; set; }

    [LeaveDayAmount(allowZero: false)]
    [Precision(7, 1)]
    public decimal RequestedRefundDays { get; set; }

    [Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
    public bool IsDirectCancellation { get; set; }

    public int? RequestedByEmployeeId { get; set; }

    [ForeignKey(nameof(RequestedByEmployeeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee? RequestedByEmployee { get; set; }

    [MaxLength(200)]
    public string? RequestedByDisplayName { get; set; }

    public LeaveRequestStatus CurrentStatus { get; set; } = LeaveRequestStatus.ManagerReview;
    public int? ManagerApproverEmployeeId { get; set; }

    [ForeignKey(nameof(ManagerApproverEmployeeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee? ManagerApprover { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];

    public ICollection<LeaveCancellationApproval> Approvals { get; set; } = [];
    public ICollection<LeaveCancellationBalanceRefund> BalanceRefunds { get; set; } = [];
}
