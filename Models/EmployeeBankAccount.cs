using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("EmployeeBankAccounts")]
[Index(nameof(EmployeeId))]
[Index(nameof(Iban), IsUnique = true)]
public sealed class EmployeeBankAccount
{
    [Key]
    public int EmployeeBankAccountId { get; set; }

    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    [InverseProperty(nameof(IK.Web.Models.Employee.BankAccounts))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(120)]
    public string BankName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? BranchName { get; set; }

    [MaxLength(30)]
    public string? BranchCode { get; set; }

    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    [Required]
    [MaxLength(34)]
    public string Iban { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }
}
