using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class PersonnelAuthorizationService(
    IDbContextFactory<HumanResourcesDbContext> dbContextFactory,
    PageAccessService pageAccessService)
{
    public async Task<PersonnelAccessDecision> ResolveAsync(
        ClaimsPrincipal principal,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        if (!pageAccessService.CanAccessPersonnelInformation(principal))
        {
            return PersonnelAccessDecision.Denied;
        }

        var actorEmployeeId = principal.GetEmployeeId();
        if (!actorEmployeeId.HasValue)
        {
            return PersonnelAccessDecision.Denied;
        }

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var targetDepartmentId = await dbContext.Employees
            .AsNoTracking()
            .Where(employee => employee.EmployeeId == employeeId)
            .Select(employee => (int?)employee.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!targetDepartmentId.HasValue)
        {
            return PersonnelAccessDecision.Denied;
        }

        if (actorEmployeeId.Value == employeeId)
        {
            return PersonnelAccessDecision.OwnEmployee;
        }

        var canViewAll = pageAccessService.HasPermission(
            principal,
            PermissionNames.CanViewAllPersonnelInformation);
        if (canViewAll)
        {
            var canEditAll = pageAccessService.HasPermission(
                principal,
                PermissionNames.CanEditAllPersonnelInformation);
            var canAccessSensitive = pageAccessService.HasPermission(
                principal,
                PermissionNames.CanAccessSensitivePersonnelInformation);
            return new PersonnelAccessDecision(
                CanView: true,
                CanEdit: canEditAll,
                CanViewSensitive: canAccessSensitive,
                CanEditSensitive: canEditAll && canAccessSensitive,
                CanPreviewManagedDocuments: true,
                CanPreviewSensitiveDocuments: canAccessSensitive,
                CanDownloadDocuments: pageAccessService.HasPermission(
                    principal,
                    PermissionNames.CanDownloadPersonnelDocuments));
        }

        var managesTargetDepartment = await dbContext.Departments
            .AsNoTracking()
            .AnyAsync(
                department =>
                    department.DepartmentId == targetDepartmentId.Value
                    && (department.ManagerEmployeeId == actorEmployeeId.Value
                        || department.ActiveDelegateEmployeeId == actorEmployeeId.Value),
                cancellationToken);
        return managesTargetDepartment
            ? PersonnelAccessDecision.ManagedEmployee
            : PersonnelAccessDecision.Denied;
    }

    public IQueryable<Employee> ApplyVisibleEmployees(
        ClaimsPrincipal principal,
        HumanResourcesDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var actorEmployeeId = principal.GetEmployeeId();
        if (!actorEmployeeId.HasValue
            || !pageAccessService.CanAccessPersonnelInformation(principal))
        {
            return dbContext.Employees.Where(_ => false);
        }

        if (pageAccessService.HasPermission(
                principal,
                PermissionNames.CanViewAllPersonnelInformation))
        {
            return dbContext.Employees;
        }

        var managedDepartmentIds = dbContext.Departments
            .Where(department =>
                department.ManagerEmployeeId == actorEmployeeId.Value
                || department.ActiveDelegateEmployeeId == actorEmployeeId.Value)
            .Select(department => department.DepartmentId);
        cancellationToken.ThrowIfCancellationRequested();
        return dbContext.Employees.Where(employee =>
            employee.EmployeeId == actorEmployeeId.Value
            || managedDepartmentIds.Contains(employee.DepartmentId));
    }

    public async Task<bool> CanSelectManagedEmployeesAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var actorEmployeeId = principal.GetEmployeeId();
        if (!actorEmployeeId.HasValue
            || !pageAccessService.CanAccessPersonnelInformation(principal))
        {
            return false;
        }

        if (pageAccessService.HasPermission(
                principal,
                PermissionNames.CanViewAllPersonnelInformation))
        {
            return true;
        }

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Departments
            .AsNoTracking()
            .AnyAsync(
                department => department.ManagerEmployeeId == actorEmployeeId.Value
                    || department.ActiveDelegateEmployeeId == actorEmployeeId.Value,
                cancellationToken);
    }
}

public sealed record PersonnelAccessDecision(
    bool CanView,
    bool CanEdit,
    bool CanViewSensitive,
    bool CanEditSensitive,
    bool CanPreviewManagedDocuments,
    bool CanPreviewSensitiveDocuments,
    bool CanDownloadDocuments)
{
    public static readonly PersonnelAccessDecision Denied = new(
        false, false, false, false, false, false, false);

    public static readonly PersonnelAccessDecision OwnEmployee = new(
        true, true, true, true, true, true, true);

    public static readonly PersonnelAccessDecision ManagedEmployee = new(
        true, false, false, false, true, false, false);

    public bool CanPreviewDocument(string categoryCanonicalKey) =>
        CanPreviewSensitiveDocuments
        || (CanPreviewManagedDocuments
            && string.Equals(
                categoryCanonicalKey,
                EmployeeDocumentCategories.Education,
                StringComparison.Ordinal));
}
