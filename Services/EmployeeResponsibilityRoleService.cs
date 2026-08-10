using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class EmployeeResponsibilityRoleService(
    HumanResourcesDbContext dbContext,
    AuditLogService auditLogService)
{
    public Task ReconcileForEmployeeActorAsync(
        IEnumerable<int> employeeIds,
        int actorEmployeeId,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (actorEmployeeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actorEmployeeId));
        }

        return ReconcileAsync(
            employeeIds,
            actorEmployeeId,
            systemActorKey: null,
            source,
            cancellationToken);
    }

    public Task ReconcileForSystemActorAsync(
        IEnumerable<int> employeeIds,
        string systemActorKey,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(systemActorKey))
        {
            throw new ArgumentException(
                "Sistem aktörü anahtarı zorunludur.",
                nameof(systemActorKey));
        }

        return ReconcileAsync(
            employeeIds,
            actorEmployeeId: null,
            systemActorKey.Trim(),
            source,
            cancellationToken);
    }

    private async Task ReconcileAsync(
        IEnumerable<int> employeeIds,
        int? actorEmployeeId,
        string? systemActorKey,
        string source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Rol değişikliği kaynağı zorunludur.", nameof(source));
        }

        var ids = employeeIds
            .Where(employeeId => employeeId > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        foreach (var employeeId in ids)
        {
            var employee = await dbContext.Employees
                .SingleOrDefaultAsync(
                    item => item.EmployeeId == employeeId,
                    cancellationToken);
            if (employee is null)
            {
                continue;
            }

            var hasManagementResponsibility = await dbContext.Departments
                .AnyAsync(
                    department => department.ManagerEmployeeId == employeeId
                                  || department.ActiveDelegateEmployeeId == employeeId,
                    cancellationToken);
            var priorRoleId = employee.ApplicationRoleId;
            if (hasManagementResponsibility
                && priorRoleId == ApplicationRoleDefaults.EmployeeRoleId)
            {
                employee.ApplicationRoleId = ApplicationRoleDefaults.AdministratorRoleId;
            }
            else if (!hasManagementResponsibility
                     && priorRoleId == ApplicationRoleDefaults.AdministratorRoleId)
            {
                employee.ApplicationRoleId = ApplicationRoleDefaults.EmployeeRoleId;
            }
            else
            {
                continue;
            }

            var details =
                $"ApplicationRoleId={priorRoleId}->{employee.ApplicationRoleId}; Source={source}; HasManagementResponsibility={hasManagementResponsibility}";
            if (actorEmployeeId.HasValue)
            {
                await auditLogService.AppendAsync(
                    AuditActionType.EmployeeUpdated,
                    nameof(Employee),
                    employee.EmployeeId.ToString(),
                    actorEmployeeId.Value,
                    details,
                    cancellationToken);
            }
            else
            {
                await auditLogService.AppendSystemAsync(
                    AuditActionType.EmployeeUpdated,
                    nameof(Employee),
                    employee.EmployeeId.ToString(),
                    systemActorKey
                        ?? throw new InvalidOperationException("Sistem aktörü bulunamadı."),
                    details,
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
