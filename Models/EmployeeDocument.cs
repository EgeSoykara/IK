using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("EmployeeDocuments")]
[Index(nameof(EmployeeId), nameof(CategoryCanonicalKey), nameof(UploadedAt))]
[Index(nameof(StorageKey), IsUnique = true)]
public sealed class EmployeeDocument
{
    [Key]
    public long EmployeeDocumentId { get; set; }

    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    [InverseProperty(nameof(Employee.Documents))]
    public Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public string CategoryCanonicalKey { get; set; } = string.Empty;

    [ForeignKey(nameof(CategoryCanonicalKey))]
    [InverseProperty(nameof(EmployeeDocumentCategory.Documents))]
    public EmployeeDocumentCategory Category { get; set; } = null!;

    public int? EmployeeIdentityDocumentId { get; set; }

    [InverseProperty(nameof(EmployeeIdentityDocument.Documents))]
    public EmployeeIdentityDocument? IdentityDocument { get; set; }

    public int? EmployeeEducationId { get; set; }

    [InverseProperty(nameof(EmployeeEducation.Documents))]
    public EmployeeEducation? Education { get; set; }

    public int? EmployeeCourseCertificateId { get; set; }

    [InverseProperty(nameof(EmployeeCourseCertificate.Documents))]
    public EmployeeCourseCertificate? CourseCertificate { get; set; }

    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string StorageKey { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    [NotMapped]
    public bool IsLinkedToRecord =>
        EmployeeIdentityDocumentId.HasValue
        || EmployeeEducationId.HasValue
        || EmployeeCourseCertificateId.HasValue;
}
