using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("LeaveCancellationBalanceRefunds")]
[Index(nameof(CancellationRequestId), nameof(BalanceId), nameof(Source), IsUnique = true)]
public sealed class LeaveCancellationBalanceRefund
{
    [Key] public int RefundId { get; set; }
    public int CancellationRequestId { get; set; }

    [ForeignKey(nameof(CancellationRequestId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public LeaveCancellationRequest CancellationRequest { get; set; } = null!;

    public int BalanceId { get; set; }
    [ForeignKey(nameof(BalanceId)), DeleteBehavior(DeleteBehavior.Restrict)]
    public LeaveBalance Balance { get; set; } = null!;
    public LeaveBalanceAllocationSource Source { get; set; }

    [LeaveDayAmount(allowZero: false)]
    [Precision(7, 1)]
    public decimal Days { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
