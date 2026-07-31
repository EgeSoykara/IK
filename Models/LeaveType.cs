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

    [Range(0d, double.MaxValue, ErrorMessage = "Annual quota cannot be negative.")]
    [Precision(7, 2)]
    public decimal AnnualQuota { get; set; }

    public bool CarryOverRule { get; set; } = true;

    [Range(0.01d, double.MaxValue, ErrorMessage = "Max accrual days must be greater than zero.")]
    [Precision(7, 2)]
    public decimal MaxAccrualDays { get; set; } = DomainConstants.MaxLeaveAccrualWarningDays;

    [Range(
        (int)LeaveEntitlementKind.Manual,
        (int)LeaveEntitlementKind.FemaleEmployees,
        ErrorMessage = "Geçersiz otomatik hak ediş kuralı.")]
    public LeaveEntitlementKind EntitlementKind { get; set; } = LeaveEntitlementKind.Manual;
}
