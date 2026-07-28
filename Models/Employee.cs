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

    public EmployeeGender? Gender { get; set; }

    public BloodGroup? BloodGroup { get; set; }

    public EmploymentStatus Status { get; set; } = EmploymentStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [InverseProperty(nameof(EmployeeProfilePhoto.Employee))]
    public EmployeeProfilePhoto? ProfilePhoto { get; set; }

    [InverseProperty(nameof(EmployeeDocument.Employee))]
    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();

    [InverseProperty(nameof(EmployeeBankAccount.Employee))]
    public ICollection<EmployeeBankAccount> BankAccounts { get; set; } = new List<EmployeeBankAccount>();

    [InverseProperty(nameof(EmployeeIdentityDocument.Employee))]
    public ICollection<EmployeeIdentityDocument> IdentityDocuments { get; set; } = new List<EmployeeIdentityDocument>();

    [InverseProperty(nameof(EmployeePhone.Employee))]
    public ICollection<EmployeePhone> Phones { get; set; } = new List<EmployeePhone>();

    [InverseProperty(nameof(EmployeeAddress.Employee))]
    public ICollection<EmployeeAddress> Addresses { get; set; } = new List<EmployeeAddress>();

    [InverseProperty(nameof(EmployeeEducation.Employee))]
    public ICollection<EmployeeEducation> Educations { get; set; } = new List<EmployeeEducation>();

    [InverseProperty(nameof(EmployeeCourseCertificate.Employee))]
    public ICollection<EmployeeCourseCertificate> CourseCertificates { get; set; } = new List<EmployeeCourseCertificate>();

    [InverseProperty(nameof(EmployeeTermination.Employee))]
    public EmployeeTermination? Termination { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
