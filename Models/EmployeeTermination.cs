using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("EmployeeTerminations")]
[Index(nameof(EmployeeId), IsUnique = true)]
public sealed class EmployeeTermination
{
    [Key]
    public int EmployeeTerminationId { get; set; }

    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    [InverseProperty(nameof(IK.Web.Models.Employee.Termination))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee Employee { get; set; } = null!;

    public DateOnly TerminationDate { get; set; }

    [Required]
    [MaxLength(160)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
