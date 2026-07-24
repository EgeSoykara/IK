using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IK.Web.Models;

[Table("EmployeeDocumentCategories")]
public sealed class EmployeeDocumentCategory
{
    [Key]
    [MaxLength(64)]
    public string CanonicalKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
}
