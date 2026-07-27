using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("EmployeePhones")]
[Index(nameof(EmployeeId))]
public sealed class EmployeePhone
{
    [Key]
    public int EmployeePhoneId { get; set; }

    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    [InverseProperty(nameof(IK.Web.Models.Employee.Phones))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(40)]
    public string PhoneType { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? Extension { get; set; }

    public bool IsPrimary { get; set; }
}
