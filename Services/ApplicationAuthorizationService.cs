using IK.Web.Database;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class ApplicationAuthorizationService(HumanResourcesDbContext dbContext)
{
    public async Task<EmployeeAuthorization?> FindForEmployeeAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var employee = await dbContext.Employees
            .AsNoTracking()
            .Include(item => item.ApplicationRole)
            .ThenInclude(role => role.Permissions)
            .SingleOrDefaultAsync(item => item.EmployeeId == employeeId, cancellationToken);

        return employee is null
            ? null
            : new EmployeeAuthorization(
                employee.ApplicationRole.ApplicationRoleId,
                employee.ApplicationRole.Name,
                employee.ApplicationRole.Permissions
                    .Select(permission => permission.PermissionName)
                    .OrderBy(permission => permission, StringComparer.Ordinal)
                    .ToArray());
    }
}

public sealed record EmployeeAuthorization(
    int ApplicationRoleId,
    string RoleName,
    IReadOnlyList<string> Permissions);
