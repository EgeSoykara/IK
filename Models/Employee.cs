using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("Employees")]
[Index(nameof(SicilNo), IsUnique = true)]
[Index(nameof(KktcKimlikNo), IsUnique = true)]
[Index(nameof(DepartmentId))]
[Index(nameof(ManagerId))]
public sealed class Employee
{
    [Key]
    public int EmployeeId { get; set; }

    [Required(ErrorMessage =  "{0} Gerekli alan.")]
    [MaxLength(30)]
    public string SicilNo { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [StringLength(10, MinimumLength = 10, ErrorMessage = "KKTC Kimlik No 10 karakter olmalıdır.")]
    [Column("KKTC_KimlikNo")]
    public string KktcKimlikNo { get; set; } = string.Empty;

    public int DepartmentId { get; set; }

    [ForeignKey(nameof(DepartmentId))]
    [InverseProperty(nameof(Department.Employees))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Department Department { get; set; } = null!;

    public int? ManagerId { get; set; }
    
    [ForeignKey(nameof(ManagerId))]
    [InverseProperty(nameof(DirectReports))]
    public Employee? Manager { get; set; }
    
    public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();
    
    public DateTime? StartDate { get; set; }

    public EmploymentStatus Status { get; set; } = EmploymentStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
