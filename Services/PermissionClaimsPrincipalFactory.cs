using System.Security.Claims;
using IK.Web.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace IK.Web.Services;

public sealed class PermissionClaimsPrincipalFactory
{
    public ClaimsPrincipal Create(
        StaticLoginUser user,
        Employee employee,
        IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.NameIdentifier, user.UserName),
            new(ClaimTypes.GivenName, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(UserClaimTypes.EmployeeId, employee.EmployeeId.ToString())
        };

        foreach (var permission in permissions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(PermissionClaimTypes.Permission, permission));
        }

        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
