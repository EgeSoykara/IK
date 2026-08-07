using System.Security.Claims;

namespace IK.Web.Services;

public static class ClaimsPrincipalExtensions
{
    public static int? GetEmployeeId(this ClaimsPrincipal? principal)
    {
        var rawEmployeeId = principal?.FindFirstValue(UserClaimTypes.EmployeeId);
        return int.TryParse(rawEmployeeId, out var employeeId) ? employeeId : null;
    }

    public static int GetRequiredEmployeeId(this ClaimsPrincipal? principal) =>
        principal.GetEmployeeId()
        ?? throw new InvalidOperationException(
            "Oturumda denetim kaydı oluşturacak çalışan kimliği bulunamadı.");

    public static string? GetDisplayName(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirstValue(ClaimTypes.GivenName)
            ?? principal?.Identity?.Name;
    }
}
