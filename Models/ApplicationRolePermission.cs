using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("ApplicationRolePermissions")]
[PrimaryKey(nameof(ApplicationRoleId), nameof(PermissionName))]
[Index(nameof(PermissionName))]
public sealed class ApplicationRolePermission
{
    public int ApplicationRoleId { get; set; }

    [ForeignKey(nameof(ApplicationRoleId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public ApplicationRole ApplicationRole { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string PermissionName { get; set; } = string.Empty;
}
