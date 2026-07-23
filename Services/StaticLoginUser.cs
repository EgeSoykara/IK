namespace IK.Web.Services;

public sealed record StaticLoginUser(
    string UserName,
    string Password,
    string DisplayName,
    int EmployeeId,
    StaticApplicationRole Role
);
