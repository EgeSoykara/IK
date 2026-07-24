namespace IK.Web.Services;

public sealed record EmployeeFileDownload(
    Stream Content,
    string ContentType,
    string? DownloadFileName);
