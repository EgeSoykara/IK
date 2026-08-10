using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("ApplicationRoles")]
[Index(nameof(Name), IsUnique = true)]
public sealed class ApplicationRole
{
    [Key]
    public int ApplicationRoleId { get; set; }

    [Required]
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(240)]
    public string? Description { get; set; }

    [InverseProperty(nameof(Employee.ApplicationRole))]
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();

    [InverseProperty(nameof(ApplicationRolePermission.ApplicationRole))]
    public ICollection<ApplicationRolePermission> Permissions { get; set; } =
        new List<ApplicationRolePermission>();
}
