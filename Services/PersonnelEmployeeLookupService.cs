using System.Security.Claims;
using IK.Web.Database;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class PersonnelEmployeeLookupService(
    IDbContextFactory<HumanResourcesDbContext> dbContextFactory,
    PageAccessService pageAccessService,
    PersonnelAuthorizationService personnelAuthorizationService)
{
    public const int MinimumSearchLength = 2;
    public const int MaximumResults = 20;
    public const int DepartmentPageSize = 8;

    public async Task<IReadOnlyList<PersonnelEmployeeOption>> SearchAsync(
        ClaimsPrincipal principal,
        string? searchText,
        CancellationToken cancellationToken = default)
    {
        if (!pageAccessService.CanAccessPersonnelInformation(principal))
        {
            throw new UnauthorizedAccessException(
                "Çalışan araması için personel yönetimi yetkisi gereklidir.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var search = searchText?.Trim();
        if (string.IsNullOrWhiteSpace(search)
            || search.Length < MinimumSearchLength)
        {
            return [];
        }

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var visibleEmployees = personnelAuthorizationService.ApplyVisibleEmployees(
            principal,
            dbContext,
            cancellationToken);
        return await visibleEmployees
            .AsNoTracking()
            .Where(employee =>
                employee.FirstName.Contains(search)
                || employee.LastName.Contains(search)
                || (employee.FirstName + " " + employee.LastName).Contains(search)
                || employee.SicilNo.Contains(search)
                || (employee.Email != null && employee.Email.Contains(search))
                || employee.Department.DepartmentName.Contains(search))
            .OrderBy(employee => employee.FirstName)
            .ThenBy(employee => employee.LastName)
            .ThenBy(employee => employee.EmployeeId)
            .Select(employee => new PersonnelEmployeeOption(
                employee.EmployeeId,
                employee.FirstName,
                employee.LastName,
                employee.SicilNo,
                employee.Email,
                employee.Department.DepartmentName))
            .Take(MaximumResults)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PersonnelDepartmentOption>> SearchDepartmentsAsync(
        ClaimsPrincipal principal,
        string? searchText,
        CancellationToken cancellationToken = default)
    {
        if (!pageAccessService.CanAccessPersonnelInformation(principal))
        {
            throw new UnauthorizedAccessException(
                "Departman araması için personel bilgilerine erişim yetkisi gereklidir.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var search = searchText?.Trim();
        if (string.IsNullOrWhiteSpace(search)
            || search.Length < MinimumSearchLength)
        {
            return [];
        }

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var visibleEmployees = personnelAuthorizationService.ApplyVisibleEmployees(
            principal,
            dbContext,
            cancellationToken);
        var visibleDepartmentIds = visibleEmployees
            .Select(employee => employee.DepartmentId);
        return await dbContext.Departments
            .AsNoTracking()
            .Where(department =>
                visibleDepartmentIds.Contains(department.DepartmentId)
                && department.DepartmentName.Contains(search))
            .OrderBy(department => department.DepartmentName)
            .ThenBy(department => department.DepartmentId)
            .Select(department => new PersonnelDepartmentOption(
                department.DepartmentId,
                department.DepartmentName))
            .Take(MaximumResults)
            .ToListAsync(cancellationToken);
    }

    public async Task<PersonnelDepartmentEmployeePage> GetDepartmentEmployeesAsync(
        ClaimsPrincipal principal,
        int departmentId,
        int pageNumber,
        CancellationToken cancellationToken = default)
    {
        if (!pageAccessService.CanAccessPersonnelInformation(principal))
        {
            throw new UnauthorizedAccessException(
                "Departman çalışanlarını görüntülemek için personel bilgilerine erişim yetkisi gereklidir.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var normalizedPageNumber = Math.Max(1, pageNumber);
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var departmentEmployees = personnelAuthorizationService.ApplyVisibleEmployees(
                principal,
                dbContext,
                cancellationToken)
            .AsNoTracking()
            .Where(employee => employee.DepartmentId == departmentId);
        var totalCount = await departmentEmployees.CountAsync(cancellationToken);
        var pageCount = Math.Max(
            1,
            (int)Math.Ceiling(totalCount / (double)DepartmentPageSize));
        normalizedPageNumber = Math.Min(normalizedPageNumber, pageCount);
        var employees = await departmentEmployees
            .OrderBy(employee => employee.FirstName)
            .ThenBy(employee => employee.LastName)
            .ThenBy(employee => employee.EmployeeId)
            .Skip((normalizedPageNumber - 1) * DepartmentPageSize)
            .Take(DepartmentPageSize)
            .Select(employee => new PersonnelEmployeeOption(
                employee.EmployeeId,
                employee.FirstName,
                employee.LastName,
                employee.SicilNo,
                employee.Email,
                employee.Department.DepartmentName))
            .ToListAsync(cancellationToken);
        return new PersonnelDepartmentEmployeePage(
            employees,
            totalCount,
            normalizedPageNumber,
            pageCount);
    }

    public async Task<PersonnelEmployeeOption?> ResolveInitialAsync(
        ClaimsPrincipal principal,
        int? requestedEmployeeId,
        CancellationToken cancellationToken = default)
    {
        if (!pageAccessService.CanAccessPersonnelInformation(principal))
        {
            throw new UnauthorizedAccessException(
                "Personel bilgilerine erişim yetkisi gereklidir.");
        }

        var currentEmployeeId = principal.GetEmployeeId();
        var preferredEmployeeId = requestedEmployeeId.HasValue
                                  && (await personnelAuthorizationService.ResolveAsync(
                                      principal,
                                      requestedEmployeeId.Value,
                                      cancellationToken)).CanView
            ? requestedEmployeeId
            : currentEmployeeId;

        var preferred = await FindAsync(
            principal,
            preferredEmployeeId,
            cancellationToken);
        if (preferred is not null || preferredEmployeeId == currentEmployeeId)
        {
            return preferred;
        }

        return await FindAsync(principal, currentEmployeeId, cancellationToken);
    }

    public async Task<PersonnelEmployeeOption?> FindAsync(
        ClaimsPrincipal principal,
        int? employeeId,
        CancellationToken cancellationToken = default)
    {
        if (!employeeId.HasValue
            || !(await personnelAuthorizationService.ResolveAsync(
                principal,
                employeeId.Value,
                cancellationToken)).CanView)
        {
            return null;
        }

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Employees
            .AsNoTracking()
            .Where(employee => employee.EmployeeId == employeeId.Value)
            .Select(employee => new PersonnelEmployeeOption(
                employee.EmployeeId,
                employee.FirstName,
                employee.LastName,
                employee.SicilNo,
                employee.Email,
                employee.Department.DepartmentName))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

public sealed record PersonnelEmployeeOption(
    int EmployeeId,
    string FirstName,
    string LastName,
    string SicilNo,
    string? Email,
    string DepartmentName)
{
    public string DisplayName =>
        $"{FirstName} {LastName} ({SicilNo}) · {DepartmentName}";
}

public sealed record PersonnelDepartmentOption(
    int DepartmentId,
    string DepartmentName);

public sealed record PersonnelDepartmentEmployeePage(
    IReadOnlyList<PersonnelEmployeeOption> Employees,
    int TotalCount,
    int PageNumber,
    int PageCount);
