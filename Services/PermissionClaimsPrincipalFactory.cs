using System.Security.Claims;
using IK.Web.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace IK.Web.Services;

public sealed class PermissionClaimsPrincipalFactory
{
    public ClaimsPrincipal Create(
        AuthenticatedUser user,
        Employee employee,
        EmployeeAuthorization authorization)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.NameIdentifier, employee.EmployeeId.ToString()),
            new(ClaimTypes.GivenName, user.DisplayName),
            new(ClaimTypes.Role, authorization.RoleName),
            new(UserClaimTypes.EmployeeId, employee.EmployeeId.ToString()),
            new(
                UserClaimTypes.MustChangePassword,
                user.RequiresPasswordChange ? bool.TrueString : bool.FalseString)
        };

        foreach (var permission in authorization.Permissions
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(PermissionClaimTypes.Permission, permission));
        }

        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
