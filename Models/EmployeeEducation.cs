using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("EmployeeEducations")]
[Index(nameof(EmployeeId))]
public sealed class EmployeeEducation
{
    [Key]
    public int EmployeeEducationId { get; set; }

    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    [InverseProperty(nameof(IK.Web.Models.Employee.Educations))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string InstitutionName { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? DepartmentName { get; set; }

    [MaxLength(120)]
    public string? Degree { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? GraduationDate { get; set; }

    public bool IsGraduated { get; set; }

    [InverseProperty(nameof(EmployeeDocument.Education))]
    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
}
