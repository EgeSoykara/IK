using System.ComponentModel.DataAnnotations;

namespace IK.Web.Services;

public sealed class ActiveDirectoryOptions
{
    public const string SectionName = "ActiveDirectory";

    [Required]
    public string Domain { get; set; } = string.Empty;
}
