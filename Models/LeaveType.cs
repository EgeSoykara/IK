using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("LeaveTypes")]
[Index(nameof(Name), IsUnique = true)]
public sealed class LeaveType
{
    [Key]
    public int LeaveTypeId { get; set; }

    [Required]
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [LeaveDayAmount]
    [Precision(7, 1)]
    public decimal AnnualQuota { get; set; }

    public bool CarryOverRule { get; set; } = true;

    [LeaveDayAmount(allowZero: false)]
    [Precision(7, 1)]
    public decimal MaxAccrualDays { get; set; } = DomainConstants.MaxLeaveAccrualWarningDays;

    [Range(
        (int)LeaveEntitlementKind.Manual,
        (int)LeaveEntitlementKind.MaleEmployees,
        ErrorMessage = "Geçersiz otomatik hak ediş kuralı.")]
    public LeaveEntitlementKind EntitlementKind { get; set; } = LeaveEntitlementKind.Manual;
}
