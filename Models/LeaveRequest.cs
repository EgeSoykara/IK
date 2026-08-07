using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("LeaveRequests")]
[Index(nameof(EmployeeId), nameof(StartDate), nameof(EndDate))]
[Index(nameof(CurrentStatus))]
[Index(nameof(Category))]
public sealed class LeaveRequest
{
    [Key]
    public int RequestId { get; set; }

    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Employee Employee { get; set; } = null!;

    [Range(
        (int)LeaveRequestCategory.AnnualLeave,
        (int)LeaveRequestCategory.SpecificLeaveType,
        ErrorMessage = "Geçersiz izin talebi kategorisi.")]
    public LeaveRequestCategory Category { get; set; }

    public int? LeaveTypeId { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LeaveType? LeaveType { get; set; }

    [Column(TypeName = "date")]
    public DateTime? StartDate { get; set; }

    [Column(TypeName = "date")]
    public DateTime? EndDate { get; set; }

    [LeaveDayAmount(allowZero: false)]
    [Precision(7, 1)]
    public decimal RequestedDays { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public LeaveRequestStatus CurrentStatus { get; set; } = LeaveRequestStatus.ManagerReview;

    public bool IsRetrospective { get; set; }

    public int? ManagerApproverEmployeeId { get; set; }

    [ForeignKey(nameof(ManagerApproverEmployeeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee? ManagerApprover { get; set; }

    public int? DelegateEmployeeId { get; set; }

    [ForeignKey(nameof(DelegateEmployeeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee? DelegateEmployee { get; set; }

    [InverseProperty(nameof(ManagerDelegation.LeaveRequest))]
    public ICollection<ManagerDelegation> ManagerDelegations { get; set; } =
        new List<ManagerDelegation>();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [InverseProperty(nameof(LeaveRequestBalanceAllocation.Request))]
    public ICollection<LeaveRequestBalanceAllocation> BalanceAllocations { get; set; } =
        new List<LeaveRequestBalanceAllocation>();

    [InverseProperty(nameof(LeaveRequestApprovedDay.Request))]
    public ICollection<LeaveRequestApprovedDay> ApprovedDays { get; set; } =
        new List<LeaveRequestApprovedDay>();

    [InverseProperty(nameof(LeaveCancellationRequest.LeaveRequest))]
    public ICollection<LeaveCancellationRequest> CancellationRequests { get; set; } =
        new List<LeaveCancellationRequest>();
}
