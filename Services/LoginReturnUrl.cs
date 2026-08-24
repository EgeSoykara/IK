namespace IK.Web.Services;

public static class LoginReturnUrl
{
    public static string? Normalize(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || !returnUrl.StartsWith("/", StringComparison.Ordinal)
            || returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            || returnUrl.Equals("/login", StringComparison.OrdinalIgnoreCase)
            || returnUrl.Equals("/auth", StringComparison.OrdinalIgnoreCase)
            || returnUrl.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return returnUrl;
    }
}
