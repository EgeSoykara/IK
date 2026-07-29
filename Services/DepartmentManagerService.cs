using System.Data;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class DepartmentManagerService(
    HumanResourcesDbContext dbContext,
    AuditLogService auditLogService)
{
    public async Task SaveDepartmentAsync(
        int? departmentId,
        string departmentName,
        int? parentDepartmentId,
        int? managerEmployeeId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        Department department;
        if (departmentId.HasValue)
        {
            department = await dbContext.Departments
                .SingleOrDefaultAsync(item => item.DepartmentId == departmentId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Departman bulunamadı.");
        }
        else
        {
            department = new Department();
            dbContext.Departments.Add(department);
        }

        var priorManagerId = department.ManagerEmployeeId;
        var priorParentDepartmentId = department.ParentDepartmentId;
        await ValidateHierarchyAsync(
            department.DepartmentId,
            parentDepartmentId,
            managerEmployeeId,
            priorManagerId,
            priorParentDepartmentId,
            cancellationToken);

        department.DepartmentName = departmentName.Trim();
        department.ParentDepartmentId = parentDepartmentId;
        department.ManagerEmployeeId = managerEmployeeId;
        await dbContext.SaveChangesAsync(cancellationToken);

        await RecomputeDepartmentAsync(department.DepartmentId, cancellationToken);
        await RecomputeChildManagersAsync(department.DepartmentId, cancellationToken);

        var action = departmentId.HasValue
            ? AuditActionType.DepartmentUpdated
            : AuditActionType.DepartmentCreated;
        await auditLogService.AppendAsync(
            action,
            nameof(Department),
            department.DepartmentId.ToString(),
            actorUserId,
            $"DepartmentName={department.DepartmentName}; ManagerChanged={priorManagerId != managerEmployeeId}");

        if (priorManagerId != managerEmployeeId)
        {
            await auditLogService.AppendAsync(
                AuditActionType.DepartmentManagerChanged,
                nameof(Department),
                department.DepartmentId.ToString(),
                actorUserId,
                "Departman yöneticisi güncellendi.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AssignEmployeeManagerAsync(
        Employee employee,
        CancellationToken cancellationToken = default)
    {
        var department = await dbContext.Departments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.DepartmentId == employee.DepartmentId, cancellationToken)
            ?? throw new InvalidOperationException("Departman seçimi zorunludur.");

        var effectiveManagerId = department.ActiveDelegateEmployeeId ?? department.ManagerEmployeeId;
        if ((department.ManagerEmployeeId == employee.EmployeeId
                || department.ActiveDelegateEmployeeId == employee.EmployeeId)
            && employee.EmployeeId != 0)
        {
            employee.ManagerId = await GetParentManagerIdAsync(department.ParentDepartmentId, cancellationToken);
            return;
        }

        employee.ManagerId = effectiveManagerId;
    }

    public async Task EnsureEmployeeCanBeUpdatedAsync(
        Employee employee,
        int departmentId,
        EmploymentStatus status,
        CancellationToken cancellationToken = default)
    {
        var managedDepartment = await dbContext.Departments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ManagerEmployeeId == employee.EmployeeId
                        || item.ActiveDelegateEmployeeId == employee.EmployeeId,
                cancellationToken);

        if (managedDepartment is not null
            && (departmentId != managedDepartment.DepartmentId || status != EmploymentStatus.Active))
        {
            throw new InvalidOperationException(
                "Departman yöneticisinin departmanı veya durumu değiştirilmeden önce departmana başka bir yönetici atanmalıdır.");
        }

        if (departmentId != employee.DepartmentId || status != EmploymentStatus.Active)
        {
            var today = DateTime.Today;
            var isAssignedDelegate = await dbContext.LeaveRequests
                .AsNoTracking()
                .AnyAsync(
                    item => item.DelegateEmployeeId == employee.EmployeeId
                            && item.EndDate != null
                            && item.EndDate.Value.Date >= today
                            && (item.CurrentStatus == LeaveRequestStatus.ManagerReview
                                || item.CurrentStatus == LeaveRequestStatus.HumanResourcesReview
                                || item.CurrentStatus == LeaveRequestStatus.Approved),
                    cancellationToken);
            if (isAssignedDelegate)
            {
                throw new InvalidOperationException(
                    "Vekil olarak atanmış çalışanın departmanı veya durumu, ilgili izin sona ermeden değiştirilemez.");
            }
        }
    }

    private async Task ValidateHierarchyAsync(
        int departmentId,
        int? parentDepartmentId,
        int? managerEmployeeId,
        int? priorManagerEmployeeId,
        int? priorParentDepartmentId,
        CancellationToken cancellationToken)
    {
        if (parentDepartmentId == departmentId && departmentId != 0)
        {
            throw new InvalidOperationException("Departman kendisinin üst departmanı olamaz.");
        }

        if (parentDepartmentId.HasValue)
        {
            var parent = await dbContext.Departments
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.DepartmentId == parentDepartmentId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Üst departman bulunamadı.");

            if (managerEmployeeId.HasValue && !parent.ManagerEmployeeId.HasValue)
            {
                throw new InvalidOperationException(
                    "Alt departmana yönetici atamadan önce üst departmanın yöneticisi belirlenmelidir.");
            }

            var cursor = parent.ParentDepartmentId;
            var visited = new HashSet<int> { parent.DepartmentId };
            while (cursor.HasValue)
            {
                if (cursor.Value == departmentId)
                {
                    throw new InvalidOperationException("Departman hiyerarşisinde döngü oluşturulamaz.");
                }

                if (!visited.Add(cursor.Value))
                {
                    throw new InvalidOperationException("Mevcut departman hiyerarşisinde döngü bulundu.");
                }

                cursor = await dbContext.Departments
                    .AsNoTracking()
                    .Where(item => item.DepartmentId == cursor.Value)
                    .Select(item => item.ParentDepartmentId)
                    .SingleOrDefaultAsync(cancellationToken);
            }
        }

        if (departmentId != 0
            && (priorManagerEmployeeId != managerEmployeeId
                || priorParentDepartmentId != parentDepartmentId))
        {
            var activeDelegationExists = await dbContext.ManagerDelegations
                .AnyAsync(
                    item => item.DepartmentId == departmentId
                            && item.RestoredAt == null,
                    cancellationToken);
            if (activeDelegationExists)
            {
                throw new InvalidOperationException(
                    "Aktif vekâlet sona ermeden departman yöneticisi değiştirilemez.");
            }
        }

        if (departmentId != 0 && !managerEmployeeId.HasValue)
        {
            var managedChildExists = await dbContext.Departments
                .AsNoTracking()
                .AnyAsync(
                    item => item.ParentDepartmentId == departmentId
                            && item.ManagerEmployeeId != null,
                    cancellationToken);
            if (managedChildExists)
            {
                throw new InvalidOperationException(
                    "Alt departmanlarda yönetici varken üst departmanın yöneticisi kaldırılamaz.");
            }
        }

        if (managerEmployeeId.HasValue)
        {
            if (departmentId == 0)
            {
                throw new InvalidOperationException(
                    "Yeni departmana yönetici atamak için önce departmanı ve çalışanı oluşturun.");
            }

            var managerIsEligible = await dbContext.Employees
                .AsNoTracking()
                .AnyAsync(
                    item => item.EmployeeId == managerEmployeeId.Value
                            && item.DepartmentId == departmentId
                            && item.Status == EmploymentStatus.Active,
                    cancellationToken);
            if (!managerIsEligible)
            {
                throw new InvalidOperationException(
                    "Departman yöneticisi aynı departmandaki aktif bir çalışan olmalıdır.");
            }

        }
    }

    private async Task RecomputeDepartmentAsync(
        int departmentId,
        CancellationToken cancellationToken)
    {
        var department = await dbContext.Departments
            .AsNoTracking()
            .SingleAsync(item => item.DepartmentId == departmentId, cancellationToken);
        var parentManagerId = await GetParentManagerIdAsync(
            department.ParentDepartmentId,
            cancellationToken);

        var employees = await dbContext.Employees
            .Where(item => item.DepartmentId == departmentId)
            .ToListAsync(cancellationToken);
        foreach (var employee in employees)
        {
            var effectiveManagerId = department.ActiveDelegateEmployeeId ?? department.ManagerEmployeeId;
            employee.ManagerId = employee.EmployeeId == department.ManagerEmployeeId
                                 || employee.EmployeeId == department.ActiveDelegateEmployeeId
                ? parentManagerId
                : effectiveManagerId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RecomputeChildManagersAsync(
        int departmentId,
        CancellationToken cancellationToken)
    {
        var parentManager = await dbContext.Departments
            .AsNoTracking()
            .Where(item => item.DepartmentId == departmentId)
            .Select(item => new { item.ManagerEmployeeId, item.ActiveDelegateEmployeeId })
            .SingleAsync(cancellationToken);
        var parentManagerId = parentManager.ActiveDelegateEmployeeId ?? parentManager.ManagerEmployeeId;

        var childManagerIds = await dbContext.Departments
            .AsNoTracking()
            .Where(item => item.ParentDepartmentId == departmentId && item.ManagerEmployeeId != null)
            .Select(item => item.ManagerEmployeeId!.Value)
            .ToListAsync(cancellationToken);

        var childManagers = await dbContext.Employees
            .Where(item => childManagerIds.Contains(item.EmployeeId))
            .ToListAsync(cancellationToken);
        foreach (var childManager in childManagers)
        {
            childManager.ManagerId = parentManagerId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<int?> GetParentManagerIdAsync(
        int? parentDepartmentId,
        CancellationToken cancellationToken)
    {
        if (!parentDepartmentId.HasValue)
        {
            return null;
        }

        return await dbContext.Departments
            .AsNoTracking()
            .Where(item => item.DepartmentId == parentDepartmentId.Value)
            .Select(item => item.ActiveDelegateEmployeeId ?? item.ManagerEmployeeId)
            .SingleAsync(cancellationToken);
    }
}
