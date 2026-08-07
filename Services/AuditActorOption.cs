namespace IK.Web.Services;

public sealed record AuditActorOption(
    int EmployeeId,
    string DisplayName,
    string SicilNo)
{
    public string Label => $"{DisplayName} ({SicilNo} · Çalışan #{EmployeeId})";
}
