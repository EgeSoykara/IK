using System.Security.Claims;

namespace IK.Web.Services;

public static class ClaimsPrincipalExtensions
{
    public static int? GetEmployeeId(this ClaimsPrincipal? principal)
    {
        var rawEmployeeId = principal?.FindFirstValue(UserClaimTypes.EmployeeId);
        return int.TryParse(rawEmployeeId, out var employeeId) ? employeeId : null;
    }

    public static string? GetDisplayName(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirstValue(ClaimTypes.GivenName)
            ?? principal?.Identity?.Name;
    }
}
