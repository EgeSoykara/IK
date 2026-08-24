namespace IK.Web.Services;

public static class EmployeeIdentityNormalizer
{
    public static string NormalizeEmail(string value) =>
        value.Trim().ToLowerInvariant();

    public static string NormalizeSamAccountName(string value) =>
        value.Trim().ToLowerInvariant();
}
