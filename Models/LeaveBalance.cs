using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("LeaveBalances")]
[Index(nameof(EmployeeId), nameof(LeaveTypeId), nameof(Year), IsUnique = true)]
[Index(nameof(Year))]
public sealed class LeaveBalance
{
    [Key]
    public int BalanceId { get; set; }

    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Employee Employee { get; set; } = null!;

    public int LeaveTypeId { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LeaveType LeaveType { get; set; } = null!;

    public int Year { get; set; }

    [Precision(7, 2)]
    public decimal EntitledDays { get; set; }

    [Precision(7, 2)]
    public decimal CarryOverDays { get; set; }

    [Precision(7, 2)]
    public decimal UsedDays { get; set; }

    [Precision(7, 2)]
    public decimal RemainingDays { get; set; }

    public bool CarryOverLimitWarningConfirmed { get; set; }

    public DateTimeOffset? CarryOverLimitWarningConfirmedAt { get; set; }

    [MaxLength(100)]
    public string? CarryOverLimitWarningConfirmedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public void RecalculateRemainingDays()
    {
        RemainingDays = EntitledDays + CarryOverDays - UsedDays;
    }
}
