using IK.Web.Database;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class ManagementAuthorizationService(HumanResourcesDbContext dbContext)
{
    public async Task<bool> IsManagerOrAboveAsync(
        int employeeId,
        int candidateManagerEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var currentManagerId = await dbContext.Employees
            .Where(employee => employee.EmployeeId == employeeId)
            .Select(employee => employee.ManagerId)
            .SingleOrDefaultAsync(cancellationToken);

        var visitedManagerIds = new HashSet<int>();
        while (currentManagerId is not null && visitedManagerIds.Add(currentManagerId.Value))
        {
            if (currentManagerId.Value == candidateManagerEmployeeId)
            {
                return true;
            }

            currentManagerId = await dbContext.Employees
                .Where(employee => employee.EmployeeId == currentManagerId.Value)
                .Select(employee => employee.ManagerId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return false;
    }

    public async Task<bool> IsRegionManagerOrAboveAsync(
        int departmentId,
        int candidateManagerEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var regionManagerEmployeeId = await dbContext.Departments
            .Where(department => department.DepartmentId == departmentId)
            .Select(department => department.RegionManagerEmployeeId)
            .SingleOrDefaultAsync(cancellationToken);

        if (regionManagerEmployeeId is null)
        {
            return false;
        }

        if (regionManagerEmployeeId.Value == candidateManagerEmployeeId)
        {
            return true;
        }

        return await IsManagerOrAboveAsync(regionManagerEmployeeId.Value, candidateManagerEmployeeId, cancellationToken);
    }
}
