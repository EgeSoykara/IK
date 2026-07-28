using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("Departments")]
[Index(nameof(DepartmentName), IsUnique = true)]
public sealed class Department
{
    [Key]
    public int DepartmentId { get; set; }

    [Required]
    [MaxLength(120)]
    public string DepartmentName { get; set; } = string.Empty;

    public int? ParentDepartmentId { get; set; }

    [ForeignKey(nameof(ParentDepartmentId))]
    [InverseProperty(nameof(ChildDepartments))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Department? ParentDepartment { get; set; }

    [InverseProperty(nameof(ParentDepartment))]
    public ICollection<Department> ChildDepartments { get; set; } = new List<Department>();

    public int? ManagerEmployeeId { get; set; }

    [ForeignKey(nameof(ManagerEmployeeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee? Manager { get; set; }

    public int? RegionManagerEmployeeId { get; set; }

    [ForeignKey(nameof(RegionManagerEmployeeId))]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public Employee? RegionManager { get; set; }

    [InverseProperty(nameof(Employee.Department))]
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
