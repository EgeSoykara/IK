using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("EmployeeProfilePhotos")]
[Index(nameof(StorageKey), IsUnique = true)]
public sealed class EmployeeProfilePhoto
{
    [Key]
    [ForeignKey(nameof(Employee))]
    public int EmployeeId { get; set; }

    [InverseProperty(nameof(IK.Web.Models.Employee.ProfilePhoto))]
    public Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string StorageKey { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
