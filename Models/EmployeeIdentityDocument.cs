using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("EmployeeIdentityDocuments")]
[Index(nameof(EmployeeId))]
[Index(nameof(EmployeeId), nameof(DocumentType), nameof(DocumentNumber), IsUnique = true)]
public sealed class EmployeeIdentityDocument
{
    [Key]
    public int EmployeeIdentityDocumentId { get; set; }

    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    [InverseProperty(nameof(IK.Web.Models.Employee.IdentityDocuments))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(80)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string DocumentNumber { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? IssuingAuthority { get; set; }

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [InverseProperty(nameof(EmployeeDocument.IdentityDocument))]
    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
}
