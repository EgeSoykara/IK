using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("LeaveCarryOverWarnings")]
[Index(nameof(BalanceId), IsUnique = true)]
[Index(nameof(IsAcknowledged), nameof(Year))]
public sealed class LeaveCarryOverWarning
{
    [Key]
    public int WarningId { get; set; }

    public int BalanceId { get; set; }

    [ForeignKey(nameof(BalanceId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public LeaveBalance Balance { get; set; } = null!;

    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee Employee { get; set; } = null!;

    public int LeaveTypeId { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LeaveType LeaveType { get; set; } = null!;

    public int Year { get; set; }

    [LeaveDayAmount]
    [Precision(7, 1)]
    public decimal CarryOverDays { get; set; }

    [LeaveDayAmount]
    [Precision(7, 1)]
    public decimal EntitledDays { get; set; }

    [LeaveDayAmount]
    [Precision(7, 1)]
    public decimal TotalDays { get; set; }

    [LeaveDayAmount(allowZero: false)]
    [Precision(7, 1)]
    public decimal WarningLimitDays { get; set; }

    public bool IsAcknowledged { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? AcknowledgedAt { get; set; }

    [MaxLength(100)]
    public string? AcknowledgedBy { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
