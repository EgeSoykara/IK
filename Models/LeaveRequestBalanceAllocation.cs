using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

public enum LeaveBalanceAllocationSource
{
    CarryOver = 1,
    Entitlement = 2
}

[Table("LeaveRequestBalanceAllocations")]
[Index(nameof(RequestId), nameof(BalanceId), nameof(Source), IsUnique = true)]
public sealed class LeaveRequestBalanceAllocation
{
    [Key]
    public int AllocationId { get; set; }

    public int RequestId { get; set; }

    [ForeignKey(nameof(RequestId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public LeaveRequest Request { get; set; } = null!;

    public int BalanceId { get; set; }

    [ForeignKey(nameof(BalanceId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LeaveBalance Balance { get; set; } = null!;

    [Range(
        (int)LeaveBalanceAllocationSource.CarryOver,
        (int)LeaveBalanceAllocationSource.Entitlement,
        ErrorMessage = "Geçersiz izin düşüm kaynağı.")]
    public LeaveBalanceAllocationSource Source { get; set; }

    [LeaveDayAmount(allowZero: false)]
    [Precision(7, 1)]
    public decimal Days { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
