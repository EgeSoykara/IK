using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("LeaveRequestApprovedDays")]
[Index(nameof(RequestId), nameof(WorkDate), IsUnique = true)]
public sealed class LeaveRequestApprovedDay
{
    [Key]
    public long ApprovedDayId { get; set; }

    public int RequestId { get; set; }

    [ForeignKey(nameof(RequestId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public LeaveRequest Request { get; set; } = null!;

    [Column(TypeName = "date")]
    public DateTime WorkDate { get; set; }

    [LeaveDayAmount(allowZero: false)]
    [Precision(2, 1)]
    public decimal Days { get; set; }
}
