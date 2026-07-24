namespace IK.Web.Services;

public sealed class EmployeeFileStorageOptions
{
    public const string SectionName = "EmployeeFiles";

    public string RootPath { get; set; } = "App_Data/employee-files";
}
