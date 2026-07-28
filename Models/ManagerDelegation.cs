using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("ManagerDelegations")]
[Index(nameof(LeaveRequestId), IsUnique = true)]
[Index(nameof(DepartmentId), nameof(IsActive))]
public sealed class ManagerDelegation
{
    [Key]
    public long ManagerDelegationId { get; set; }

    public int LeaveRequestId { get; set; }

    [ForeignKey(nameof(LeaveRequestId))]
    [InverseProperty(nameof(Models.LeaveRequest.ManagerDelegation))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LeaveRequest LeaveRequest { get; set; } = null!;

    public int DepartmentId { get; set; }

    [ForeignKey(nameof(DepartmentId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Department Department { get; set; } = null!;

    public int ManagerEmployeeId { get; set; }

    [ForeignKey(nameof(ManagerEmployeeId))]
    [InverseProperty(nameof(Employee.ManagerDelegations))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee ManagerEmployee { get; set; } = null!;

    public int DelegateEmployeeId { get; set; }

    [ForeignKey(nameof(DelegateEmployeeId))]
    [InverseProperty(nameof(Employee.DelegateAssignments))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee DelegateEmployee { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public DateTimeOffset? RestoredAt { get; set; }
}
