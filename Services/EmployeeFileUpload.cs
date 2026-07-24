namespace IK.Web.Services;

public sealed record EmployeeFileUpload(
    Stream Content,
    string FileName,
    long Length);
