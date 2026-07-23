using System.Security.Claims;
using IK.Web.Database;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class PageAccessService(HumanResourcesDbContext dbContext)
{
    public bool CanAccessDashboard(ClaimsPrincipal? principal)
    {
        return CanAccessAuthenticatedPages(principal);
    }

    public bool CanAccessDashboardEmployee(
        ClaimsPrincipal? principal,
        int? requestedEmployeeId)
    {
        if (!CanAccessDashboard(principal))
        {
            return false;
        }

        var currentEmployeeId = principal.GetEmployeeId();

        if (requestedEmployeeId is null || requestedEmployeeId == currentEmployeeId)
        {
            return true;
        }

        return CanSearchEmployees(principal) || CanManageEmployees(principal);
    }

    public bool HasPermission(ClaimsPrincipal? principal, string permission)
    {
        return principal?.HasClaim(PermissionClaimTypes.Permission, permission) == true;
    }

    public bool CanAccessAuthenticatedPages(ClaimsPrincipal? principal)
    {
        return principal?.Identity?.IsAuthenticated == true;
    }

    public async Task<bool> CanAccessDepartmentsAsync(
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)

    {
        if (CanAccessAuthenticatedPages(principal))
        {
            return CanManageDepartments(principal);
        }

        return !await dbContext.Departments.AnyAsync(cancellationToken);
    }

    public async Task<bool> CanAccessEmployeesAsync(
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)
    {
        if (CanAccessAuthenticatedPages(principal))
        {
            return CanSearchEmployees(principal) || CanManageEmployees(principal);
        }

        return !await dbContext.Employees.AnyAsync(cancellationToken);
    }

    public Task<bool> CanAccessLeaveApprovalsAsync(
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)
    {
        if (CanAccessAuthenticatedPages(principal))
        {
            return Task.FromResult(CanExecuteApproveLeave(principal));
        }

        return Task.FromResult(false);
    }
    public Task<bool> CanAccessLeaveBalanceAsync(
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)
    {
        if (CanAccessAuthenticatedPages(principal))
        {
            return Task.FromResult(CanManageLeaveBalances(principal));
        }

        return Task.FromResult(false);
    }
    public Task<bool> CanAccessLeaveRequestsAsync(
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)
    {
        if (CanAccessAuthenticatedPages(principal))
        {
            return Task.FromResult(
                CanViewLeaveRequests(principal)
                || CanManageLeaveRequests(principal)
                || CanEditDeleteLeaveRequests(principal));
        }

        return Task.FromResult(false);
    }
    public Task<bool> CanAccessLeaveTypesAsync(
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)
    {
        if (CanAccessAuthenticatedPages(principal))
        {
            return Task.FromResult(CanManageLeaveTypes(principal));
        }

        return Task.FromResult(false);
    }
    public Task<bool> CanAccessAuditLogsAsync(
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)
    {
        if (CanAccessAuthenticatedPages(principal))
        {
            return Task.FromResult(CanViewAuditLogs(principal));
        }

        return Task.FromResult(false);
    }

    public bool CanSearchEmployees(ClaimsPrincipal? principal)
    {
        return CanAccessAuthenticatedPages(principal)
            && HasPermission(principal, PermissionNames.CanviewEmployeeSearch);
    }

    public bool CanManageDepartments(ClaimsPrincipal? principal)
    {
        return CanAccessAuthenticatedPages(principal)
            && HasPermission(principal, PermissionNames.CanManageDepartments);
    }

    public bool CanManageEmployees(ClaimsPrincipal? principal)
    {
        return CanAccessAuthenticatedPages(principal)
            && HasPermission(principal, PermissionNames.CanCreateNewEmployee);
    }

    public bool CanManageLeaveTypes(ClaimsPrincipal? principal)
    {
        return CanAccessAuthenticatedPages(principal)
            && HasPermission(principal, PermissionNames.CanManageLeaveTypes);
    }

    public bool CanManageLeaveBalances(ClaimsPrincipal? principal)
    {
        return CanAccessAuthenticatedPages(principal)
            && HasPermission(principal, PermissionNames.CanManageLeaveBalances);
    }

    public bool CanManageLeaveRequests(ClaimsPrincipal? principal)
    {
        return CanAccessAuthenticatedPages(principal)
            && HasPermission(principal, PermissionNames.CanManageLeaveRequests);
    }

    public bool CanViewLeaveRequests(ClaimsPrincipal? principal)
    {
        return CanAccessAuthenticatedPages(principal)
            && HasPermission(principal, PermissionNames.CanViewLeaveRequests);
    }

    public bool CanEditDeleteLeaveRequests(ClaimsPrincipal? principal)
    {
        return CanAccessAuthenticatedPages(principal)
            && HasPermission(principal, PermissionNames.CanEditDeleteLeaveRequests);
    }

    public bool CanExecuteApproveLeave(ClaimsPrincipal? principal)
    {
        return CanAccessAuthenticatedPages(principal)
            && HasPermission(principal, PermissionNames.CanExectuteApproveLeave);
    }

    public bool CanViewAuditLogs(ClaimsPrincipal? principal)
    {
        return CanAccessAuthenticatedPages(principal)
            && HasPermission(principal, PermissionNames.CanViewAuditLogs);
    }
}
