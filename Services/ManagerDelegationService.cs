using System.Data;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class ManagerDelegationService(
    HumanResourcesDbContext dbContext,
    AuditLogService auditLogService,
    EmployeeResponsibilityRoleService responsibilityRoleService,
    TimeProvider timeProvider)
{
    public async Task<ActiveDelegationTransferContext?> GetTransferContextAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var department = await dbContext.Departments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ActiveDelegateEmployeeId == employeeId,
                cancellationToken);
        if (department is null)
        {
            return null;
        }

        var openChain = await dbContext.ManagerDelegations
            .AsNoTracking()
            .Where(item => item.DepartmentId == department.DepartmentId
                           && item.RestoredAt == null)
            .ToListAsync(cancellationToken);
        var currentRecord = openChain
            .Where(item => item.DelegateEmployeeId == employeeId)
            .OrderByDescending(item => item.ActivatedAt)
            .FirstOrDefault();
        var previousDelegateId = currentRecord is
            {
                ParentManagerDelegationId: not null,
                LeaveRequestId: null
            }
                ? currentRecord.ManagerEmployeeId
                : (int?)null;
        var unavailableIds = openChain
            .SelectMany(item => new[] { item.ManagerEmployeeId, item.DelegateEmployeeId })
            .Append(department.ManagerEmployeeId ?? 0)
            .ToHashSet();
        if (previousDelegateId.HasValue)
        {
            unavailableIds.Remove(previousDelegateId.Value);
        }
        var candidates = await dbContext.Employees
            .AsNoTracking()
            .Where(item => item.DepartmentId == department.DepartmentId
                           && item.Status == EmploymentStatus.Active
                           && !unavailableIds.Contains(item.EmployeeId))
            .OrderBy(item => item.FirstName)
            .ThenBy(item => item.LastName)
            .Select(item => new ActiveDelegationCandidate(
                item.EmployeeId,
                $"{item.FirstName} {item.LastName}",
                item.SicilNo))
            .ToListAsync(cancellationToken);

        return new ActiveDelegationTransferContext(
            department.DepartmentId,
            department.DepartmentName,
            candidates);
    }

    public async Task ValidateSelectionAsync(
        int managerEmployeeId,
        int delegateEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var department = await FindManagedDepartmentAsync(managerEmployeeId, cancellationToken)
            ?? throw new InvalidOperationException("Vekâlet verilebilecek yönetilen departman bulunamadı.");

        await EnsureDelegateEligibleAsync(
            department,
            managerEmployeeId,
            delegateEmployeeId,
            cancellationToken);
    }

    public async Task TransferActiveDelegationAsync(
        int departmentId,
        int actorEmployeeId,
        int newDelegateEmployeeId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var department = await dbContext.Departments
            .SingleOrDefaultAsync(item => item.DepartmentId == departmentId, cancellationToken)
            ?? throw new InvalidOperationException("Departman bulunamadı.");

        if (department.ActiveDelegateEmployeeId != actorEmployeeId)
        {
            throw new InvalidOperationException(
                "Vekâleti yalnız departmanın mevcut aktif vekili devredebilir.");
        }

        var currentRecord = await dbContext.ManagerDelegations
            .Where(item => item.DepartmentId == departmentId
                           && item.DelegateEmployeeId == actorEmployeeId
                           && item.RestoredAt == null)
            .OrderByDescending(item => item.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Aktif vekâlet kaydı bulunamadı.");

        var rootRecord = await dbContext.ManagerDelegations
            .Where(item => item.DepartmentId == departmentId
                           && item.ParentManagerDelegationId == null
                           && item.RestoredAt == null)
            .OrderBy(item => item.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Ana vekâlet kaydı bulunamadı.");
        var localToday = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        if (!await IsLeaveActiveAsync(rootRecord.LeaveRequestId, localToday, cancellationToken))
        {
            throw new InvalidOperationException(
                "Ana yöneticinin izni sona erdiği için vekâlet devredilemez.");
        }

        var now = timeProvider.GetUtcNow();
        if (currentRecord.ParentManagerDelegationId.HasValue
            && currentRecord.LeaveRequestId is null
            && currentRecord.ManagerEmployeeId == newDelegateEmployeeId)
        {
            await EnsureEmployeeActiveInDepartmentAsync(
                department,
                newDelegateEmployeeId,
                cancellationToken);
            currentRecord.RestoredAt = now;
            department.ActiveDelegateEmployeeId = newDelegateEmployeeId;
            await ApplyEffectiveManagerAsync(
                department,
                actorEmployeeId,
                newDelegateEmployeeId,
                cancellationToken);
            await auditLogService.AppendAsync(
                AuditActionType.ManagerDelegationTransferred,
                nameof(ManagerDelegation),
                departmentId.ToString(),
                actorEmployeeId,
                "Aktif vekâlet önceki aktif vekile geri devredildi.",
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await responsibilityRoleService.ReconcileForEmployeeActorAsync(
                [actorEmployeeId, newDelegateEmployeeId],
                actorEmployeeId,
                "ManagerDelegationTransferred",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await EnsureDelegateEligibleAsync(
            department,
            actorEmployeeId,
            newDelegateEmployeeId,
            cancellationToken);

        var record = new ManagerDelegation
        {
            DepartmentId = departmentId,
            ParentManagerDelegationId = currentRecord.ManagerDelegationId,
            ManagerEmployeeId = actorEmployeeId,
            DelegateEmployeeId = newDelegateEmployeeId,
            StartDate = localToday,
            EndDate = currentRecord.EndDate,
            ActivatedAt = now
        };
        dbContext.ManagerDelegations.Add(record);
        department.ActiveDelegateEmployeeId = newDelegateEmployeeId;

        await ApplyEffectiveManagerAsync(
            department,
            actorEmployeeId,
            newDelegateEmployeeId,
            cancellationToken);
        await auditLogService.AppendAsync(
            AuditActionType.ManagerDelegationTransferred,
            nameof(ManagerDelegation),
            departmentId.ToString(),
            actorEmployeeId,
            "Aktif vekâlet başka bir uygun çalışana devredildi.",
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await responsibilityRoleService.ReconcileForEmployeeActorAsync(
            [actorEmployeeId, newDelegateEmployeeId],
            actorEmployeeId,
            "ManagerDelegationTransferred",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReconcileAsync(
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var delegatedDepartments = await dbContext.Departments
            .Where(item => item.ActiveDelegateEmployeeId != null)
            .ToListAsync(cancellationToken);

        foreach (var department in delegatedDepartments)
        {
            await ReconcileDepartmentAsync(department, today, cancellationToken);
        }

        var eligibleLeaves = await dbContext.LeaveRequests
            .AsNoTracking()
            .Where(item => item.CurrentStatus == LeaveRequestStatus.Approved
                           && item.DelegateEmployeeId != null
                           && item.StartDate != null
                           && item.EndDate != null
                           && item.StartDate.Value.Date <= today.ToDateTime(TimeOnly.MinValue)
                           && item.EndDate.Value.Date >= today.ToDateTime(TimeOnly.MinValue)
                           && !item.CancellationRequests.Any(cancellation =>
                               cancellation.CurrentStatus == LeaveRequestStatus.Approved
                               && cancellation.CancellationStartDate <= today.ToDateTime(TimeOnly.MinValue)
                               && cancellation.CancellationEndDate >= today.ToDateTime(TimeOnly.MinValue)))
            .OrderBy(item => item.StartDate)
            .ThenBy(item => item.RequestId)
            .ToListAsync(cancellationToken);

        foreach (var leave in eligibleLeaves)
        {
            if (await dbContext.ManagerDelegations
                    .AnyAsync(item => item.LeaveRequestId == leave.RequestId
                                      && item.RestoredAt == null, cancellationToken))
            {
                continue;
            }

            var department = await FindManagedDepartmentAsync(leave.EmployeeId, cancellationToken);
            if (department is null)
            {
                continue;
            }

            ManagerDelegation? parent = null;
            if (department.ManagerEmployeeId == leave.EmployeeId)
            {
                if (department.ActiveDelegateEmployeeId.HasValue)
                {
                    continue;
                }
            }
            else
            {
                if (department.ActiveDelegateEmployeeId != leave.EmployeeId)
                {
                    continue;
                }

                parent = await FindCurrentRecordAsync(department.DepartmentId, leave.EmployeeId, cancellationToken);
                if (parent is null)
                {
                    continue;
                }
            }

            try
            {
                await EnsureDelegateEligibleAsync(
                    department,
                    leave.EmployeeId,
                    leave.DelegateEmployeeId!.Value,
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            var record = new ManagerDelegation
            {
                LeaveRequestId = leave.RequestId,
                ParentManagerDelegationId = parent?.ManagerDelegationId,
                DepartmentId = department.DepartmentId,
                ManagerEmployeeId = leave.EmployeeId,
                DelegateEmployeeId = leave.DelegateEmployeeId.Value,
                StartDate = DateOnly.FromDateTime(leave.StartDate!.Value),
                EndDate = DateOnly.FromDateTime(leave.EndDate!.Value),
                ActivatedAt = timeProvider.GetUtcNow()
            };
            dbContext.ManagerDelegations.Add(record);
            department.ActiveDelegateEmployeeId = leave.DelegateEmployeeId.Value;

            await ApplyEffectiveManagerAsync(
                department,
                leave.EmployeeId,
                leave.DelegateEmployeeId.Value,
                cancellationToken);
            await auditLogService.AppendSystemAsync(
                AuditActionType.ManagerDelegationActivated,
                nameof(ManagerDelegation),
                leave.RequestId.ToString(),
                "System",
                "Yönetici vekâleti etkinleştirildi.",
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await responsibilityRoleService.ReconcileForSystemActorAsync(
                [leave.EmployeeId, leave.DelegateEmployeeId.Value],
                "System",
                "ManagerDelegationActivated",
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ReconcileDepartmentAsync(
        Department department,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var root = await dbContext.ManagerDelegations
            .Where(item => item.DepartmentId == department.DepartmentId
                           && item.ParentManagerDelegationId == null
                           && item.RestoredAt == null)
            .OrderBy(item => item.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (root is null
            || !await IsLeaveActiveAsync(root.LeaveRequestId, today, cancellationToken))
        {
            await RestoreWholeDepartmentAsync(department, cancellationToken);
            return;
        }

        while (department.ActiveDelegateEmployeeId.HasValue)
        {
            var current = await FindCurrentRecordAsync(
                department.DepartmentId,
                department.ActiveDelegateEmployeeId.Value,
                cancellationToken);
            if (current is null)
            {
                await RestoreWholeDepartmentAsync(department, cancellationToken);
                return;
            }

            var delegateEligible = await dbContext.Employees
                .AsNoTracking()
                .AnyAsync(
                    item => item.EmployeeId == current.DelegateEmployeeId
                            && item.DepartmentId == department.DepartmentId
                            && item.Status == EmploymentStatus.Active,
                    cancellationToken);
            var currentLeaveActive = current.LeaveRequestId is null
                || await IsLeaveActiveAsync(current.LeaveRequestId, today, cancellationToken);
            if (delegateEligible && currentLeaveActive)
            {
                return;
            }

            await RestoreLeafAsync(department, current, cancellationToken);
        }
    }

    private async Task RestoreLeafAsync(
        Department department,
        ManagerDelegation current,
        CancellationToken cancellationToken)
    {
        current.RestoredAt = timeProvider.GetUtcNow();
        var restoredManagerId = current.ParentManagerDelegationId.HasValue
            ? current.ManagerEmployeeId
            : department.ManagerEmployeeId;
        department.ActiveDelegateEmployeeId = current.ParentManagerDelegationId.HasValue
            ? current.ManagerEmployeeId
            : null;

        await ApplyEffectiveManagerAsync(
            department,
            current.DelegateEmployeeId,
            restoredManagerId,
            cancellationToken);
        await auditLogService.AppendSystemAsync(
            AuditActionType.ManagerDelegationRestored,
            nameof(ManagerDelegation),
            (current.LeaveRequestId?.ToString() ?? current.ManagerDelegationId.ToString()),
            "System",
            "Yönetici vekâleti önceki yöneticiye döndürüldü.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await responsibilityRoleService.ReconcileForSystemActorAsync(
            [current.DelegateEmployeeId, restoredManagerId ?? 0],
            "System",
            "ManagerDelegationRestored",
            cancellationToken);
    }

    private async Task RestoreWholeDepartmentAsync(
        Department department,
        CancellationToken cancellationToken)
    {
        var currentDelegateId = department.ActiveDelegateEmployeeId;
        var openRecords = await dbContext.ManagerDelegations
            .Where(item => item.DepartmentId == department.DepartmentId
                           && item.RestoredAt == null)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var record in openRecords)
        {
            record.RestoredAt = now;
        }

        department.ActiveDelegateEmployeeId = null;
        await ApplyEffectiveManagerAsync(
            department,
            currentDelegateId,
            department.ManagerEmployeeId,
            cancellationToken);
        await auditLogService.AppendSystemAsync(
            AuditActionType.ManagerDelegationRestored,
            nameof(ManagerDelegation),
            department.DepartmentId.ToString(),
            "System",
            "Ana yönetici döndüğü için aktif vekâlet zinciri kapatıldı.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await responsibilityRoleService.ReconcileForSystemActorAsync(
            [currentDelegateId ?? 0, department.ManagerEmployeeId ?? 0],
            "System",
            "ManagerDelegationRestored",
            cancellationToken);
    }

    private async Task ApplyEffectiveManagerAsync(
        Department department,
        int? previousEffectiveManagerId,
        int? effectiveManagerId,
        CancellationToken cancellationToken)
    {
        var parentManagerId = department.ParentDepartmentId.HasValue
            ? await dbContext.Departments
                .AsNoTracking()
                .Where(item => item.DepartmentId == department.ParentDepartmentId.Value)
                .Select(item => item.ActiveDelegateEmployeeId ?? item.ManagerEmployeeId)
                .SingleAsync(cancellationToken)
            : null;

        var employees = await dbContext.Employees
            .Where(item => item.DepartmentId == department.DepartmentId)
            .ToListAsync(cancellationToken);
        foreach (var employee in employees)
        {
            employee.ManagerId = employee.EmployeeId == effectiveManagerId
                                 || employee.EmployeeId == department.ManagerEmployeeId
                ? parentManagerId
                : effectiveManagerId;
        }

        if (previousEffectiveManagerId.HasValue && effectiveManagerId.HasValue)
        {
            var pendingApprovals = await dbContext.LeaveRequests
                .Where(item => item.ManagerApproverEmployeeId == previousEffectiveManagerId
                               && item.CurrentStatus == LeaveRequestStatus.ManagerReview)
                .ToListAsync(cancellationToken);
            foreach (var request in pendingApprovals)
            {
                request.ManagerApproverEmployeeId = effectiveManagerId;
            }
        }

        var childManagerIds = await dbContext.Departments
            .AsNoTracking()
            .Where(item => item.ParentDepartmentId == department.DepartmentId)
            .Select(item => item.ActiveDelegateEmployeeId ?? item.ManagerEmployeeId)
            .Where(item => item != null)
            .Select(item => item!.Value)
            .ToListAsync(cancellationToken);
        var childManagers = await dbContext.Employees
            .Where(item => childManagerIds.Contains(item.EmployeeId))
            .ToListAsync(cancellationToken);
        foreach (var childManager in childManagers)
        {
            childManager.ManagerId = effectiveManagerId;
        }
    }

    private async Task EnsureDelegateEligibleAsync(
        Department department,
        int managerEmployeeId,
        int delegateEmployeeId,
        CancellationToken cancellationToken)
    {
        if (managerEmployeeId == delegateEmployeeId
            || department.ManagerEmployeeId == delegateEmployeeId)
        {
            throw new InvalidOperationException("Yönetici kendisini veya ana yöneticiyi vekil olarak seçemez.");
        }

        var openChain = await dbContext.ManagerDelegations
            .AsNoTracking()
            .Where(item => item.DepartmentId == department.DepartmentId
                           && item.RestoredAt == null)
            .ToListAsync(cancellationToken);
        var chainEmployeeIds = openChain
            .SelectMany(item => new[] { item.ManagerEmployeeId, item.DelegateEmployeeId })
            .ToHashSet();
        if (chainEmployeeIds.Contains(delegateEmployeeId))
        {
            throw new InvalidOperationException("Vekâlet zincirinde döngü oluşturulamaz.");
        }

        await EnsureEmployeeActiveInDepartmentAsync(
            department,
            delegateEmployeeId,
            cancellationToken);
    }

    private async Task EnsureEmployeeActiveInDepartmentAsync(
        Department department,
        int employeeId,
        CancellationToken cancellationToken)
    {
        var eligible = await dbContext.Employees
            .AsNoTracking()
            .AnyAsync(
                item => item.EmployeeId == employeeId
                        && item.DepartmentId == department.DepartmentId
                        && item.Status == EmploymentStatus.Active,
                cancellationToken);
        if (!eligible)
        {
            throw new InvalidOperationException(
                "Vekil aynı departmandaki aktif bir çalışan olmalıdır.");
        }
    }

    private Task<Department?> FindManagedDepartmentAsync(
        int managerEmployeeId,
        CancellationToken cancellationToken) =>
        dbContext.Departments.SingleOrDefaultAsync(
            item => item.ManagerEmployeeId == managerEmployeeId
                    || item.ActiveDelegateEmployeeId == managerEmployeeId,
            cancellationToken);

    private Task<ManagerDelegation?> FindCurrentRecordAsync(
        int departmentId,
        int delegateEmployeeId,
        CancellationToken cancellationToken) =>
        dbContext.ManagerDelegations
            .Where(item => item.DepartmentId == departmentId
                           && item.DelegateEmployeeId == delegateEmployeeId
                           && item.RestoredAt == null)
            .OrderByDescending(item => item.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<bool> IsLeaveActiveAsync(
        int? leaveRequestId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (!leaveRequestId.HasValue)
        {
            return Task.FromResult(false);
        }

        var todayDate = today.ToDateTime(TimeOnly.MinValue);
        return dbContext.LeaveRequests
            .AsNoTracking()
            .AnyAsync(
                item => item.RequestId == leaveRequestId.Value
                        && item.CurrentStatus == LeaveRequestStatus.Approved
                        && item.StartDate != null
                        && item.EndDate != null
                        && item.StartDate.Value.Date <= todayDate
                        && item.EndDate.Value.Date >= todayDate
                        && !item.CancellationRequests.Any(cancellation =>
                            cancellation.CurrentStatus == LeaveRequestStatus.Approved
                            && cancellation.CancellationStartDate <= todayDate
                            && cancellation.CancellationEndDate >= todayDate),
                cancellationToken);
    }
}

public sealed record ActiveDelegationTransferContext(
    int DepartmentId,
    string DepartmentName,
    IReadOnlyList<ActiveDelegationCandidate> Candidates);

public sealed record ActiveDelegationCandidate(
    int EmployeeId,
    string EmployeeName,
    string SicilNo);
