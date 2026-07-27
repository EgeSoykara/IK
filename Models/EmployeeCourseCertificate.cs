using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("EmployeeCourseCertificates")]
[Index(nameof(EmployeeId))]
public sealed class EmployeeCourseCertificate
{
    [Key]
    public int EmployeeCourseCertificateId { get; set; }

    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    [InverseProperty(nameof(IK.Web.Models.Employee.CourseCertificates))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? IssuingOrganization { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [InverseProperty(nameof(EmployeeDocument.CourseCertificate))]
    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
}
