using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Models;

[Table("PublicHolidays")]
[Index(nameof(Date), IsUnique = true)]
public sealed class PublicHoliday
{
    [Key]
    public int PublicHolidayId { get; set; }

    public DateOnly Date { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;
}
