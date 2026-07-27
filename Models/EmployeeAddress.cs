using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("EmployeeAddresses")]
[Index(nameof(EmployeeId))]
public sealed class EmployeeAddress
{
    [Key]
    public int EmployeeAddressId { get; set; }

    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    [InverseProperty(nameof(IK.Web.Models.Employee.Addresses))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(40)]
    public string AddressType { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string AddressLine { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? District { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    public bool IsPrimary { get; set; }
}
