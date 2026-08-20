using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("EmployeeCredentials")]
public sealed class EmployeeCredential
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int EmployeeId { get; set; }

    [Required]
    [MaxLength(512)]
    public string PasswordHash { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; } = true;

    public DateTimeOffset? PasswordChangedAt { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Employee Employee { get; set; } = null!;
}
