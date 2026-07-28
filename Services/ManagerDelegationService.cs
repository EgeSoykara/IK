using System.Data;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class ManagerDelegationService(
    HumanResourcesDbContext dbContext,
    AuditLogService auditLogService)
{
    public async Task ValidateSelectionAsync(
        int managerEmployeeId,
        int delegateEmployeeId,
        CancellationToken cancellationToken = default)
    {
        if (managerEmployeeId == delegateEmployeeId)
        {
            throw new InvalidOperationException("Yönetici kendisini vekil olarak seçemez.");
        }

        if (!await IsSelectionEligibleAsync(
                managerEmployeeId,
                delegateEmployeeId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Vekil aynı departmandaki aktif bir çalışan olmalıdır.");
        }
    }

    public async Task ReconcileAsync(
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var activeRecords = await dbContext.ManagerDelegations
            .Where(item => item.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var delegation in activeRecords)
        {
            var selectionStillEligible = await IsSelectionEligibleAsync(
                delegation.ManagerEmployeeId,
                delegation.DelegateEmployeeId,
                cancellationToken);
            var leaveStillEligible = await dbContext.LeaveRequests
                .AsNoTracking()
                .AnyAsync(
                    item => item.RequestId == delegation.LeaveRequestId
                            && item.CurrentStatus == LeaveRequestStatus.Approved
                            && item.StartDate != null
                            && item.EndDate != null
                            && DateOnly.FromDateTime(item.StartDate.Value) <= today
                            && DateOnly.FromDateTime(item.EndDate.Value) >= today,
                    cancellationToken);
            if (!leaveStillEligible || !selectionStillEligible)
            {
                await RestoreAsync(delegation, cancellationToken);
            }
        }

        var eligibleLeaves = await dbContext.LeaveRequests
            .AsNoTracking()
            .Where(item => item.CurrentStatus == LeaveRequestStatus.Approved
                           && item.DelegateEmployeeId != null
                           && item.StartDate != null
                           && item.EndDate != null
                           && item.StartDate.Value.Date <= today.ToDateTime(TimeOnly.MinValue)
                           && item.EndDate.Value.Date >= today.ToDateTime(TimeOnly.MinValue))
            .OrderBy(item => item.StartDate)
            .ToListAsync(cancellationToken);

        foreach (var leave in eligibleLeaves)
        {
            var alreadyActive = await dbContext.ManagerDelegations
                .AnyAsync(item => item.LeaveRequestId == leave.RequestId && item.IsActive, cancellationToken);
            if (alreadyActive)
            {
                continue;
            }

            var department = await dbContext.Departments
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.ManagerEmployeeId == leave.EmployeeId,
                    cancellationToken);
            if (department is null)
            {
                continue;
            }

            var competingDelegation = await dbContext.ManagerDelegations
                .AnyAsync(
                    item => item.DepartmentId == department.DepartmentId && item.IsActive,
                    cancellationToken);
            if (competingDelegation)
            {
                continue;
            }

            if (!await IsSelectionEligibleAsync(
                    leave.EmployeeId,
                    leave.DelegateEmployeeId!.Value,
                    cancellationToken))
            {
                continue;
            }

            var record = await dbContext.ManagerDelegations
                .SingleOrDefaultAsync(item => item.LeaveRequestId == leave.RequestId, cancellationToken);
            if (record is null)
            {
                record = new ManagerDelegation
                {
                    LeaveRequestId = leave.RequestId,
                    DepartmentId = department.DepartmentId,
                    ManagerEmployeeId = leave.EmployeeId,
                    DelegateEmployeeId = leave.DelegateEmployeeId.Value,
                    StartDate = DateOnly.FromDateTime(leave.StartDate!.Value),
                    EndDate = DateOnly.FromDateTime(leave.EndDate!.Value)
                };
                dbContext.ManagerDelegations.Add(record);
            }

            record.IsActive = true;
            record.ActivatedAt = DateTimeOffset.UtcNow;
            record.RestoredAt = null;

            var directReports = await dbContext.Employees
                .Where(item => item.ManagerId == leave.EmployeeId
                               && item.EmployeeId != leave.DelegateEmployeeId.Value)
                .ToListAsync(cancellationToken);
            foreach (var directReport in directReports)
            {
                directReport.ManagerId = leave.DelegateEmployeeId.Value;
            }

            var pendingApprovals = await dbContext.LeaveRequests
                .Where(item => item.ManagerApproverEmployeeId == leave.EmployeeId
                               && item.CurrentStatus == LeaveRequestStatus.ManagerReview)
                .ToListAsync(cancellationToken);
            foreach (var pendingApproval in pendingApprovals)
            {
                pendingApproval.ManagerApproverEmployeeId = leave.DelegateEmployeeId.Value;
            }

            await auditLogService.AppendAsync(
                AuditActionType.ManagerDelegationActivated,
                nameof(ManagerDelegation),
                leave.RequestId.ToString(),
                "System",
                "Yönetici vekâleti etkinleştirildi.");
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<bool> IsSelectionEligibleAsync(
        int managerEmployeeId,
        int delegateEmployeeId,
        CancellationToken cancellationToken)
    {
        if (managerEmployeeId == delegateEmployeeId)
        {
            return false;
        }

        var departmentId = await dbContext.Departments
            .AsNoTracking()
            .Where(item => item.ManagerEmployeeId == managerEmployeeId)
            .Select(item => (int?)item.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!departmentId.HasValue)
        {
            return false;
        }

        return await dbContext.Employees
            .AsNoTracking()
            .AnyAsync(
                item => item.EmployeeId == delegateEmployeeId
                        && item.DepartmentId == departmentId.Value
                        && item.Status == EmploymentStatus.Active,
                cancellationToken);
    }

    private async Task RestoreAsync(
        ManagerDelegation delegation,
        CancellationToken cancellationToken)
    {
        var delegatedReports = await dbContext.Employees
            .Where(item => item.ManagerId == delegation.DelegateEmployeeId
                           && item.EmployeeId != delegation.ManagerEmployeeId)
            .ToListAsync(cancellationToken);
        foreach (var employee in delegatedReports)
        {
            employee.ManagerId = delegation.ManagerEmployeeId;
        }

        var pendingApprovals = await dbContext.LeaveRequests
            .Where(item => item.ManagerApproverEmployeeId == delegation.DelegateEmployeeId
                           && item.CurrentStatus == LeaveRequestStatus.ManagerReview)
            .ToListAsync(cancellationToken);
        foreach (var request in pendingApprovals)
        {
            request.ManagerApproverEmployeeId = delegation.ManagerEmployeeId;
        }

        delegation.IsActive = false;
        delegation.RestoredAt = DateTimeOffset.UtcNow;
        await auditLogService.AppendAsync(
            AuditActionType.ManagerDelegationRestored,
            nameof(ManagerDelegation),
            delegation.LeaveRequestId.ToString(),
            "System",
            "Yönetici vekâleti sona erdirildi.");
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
