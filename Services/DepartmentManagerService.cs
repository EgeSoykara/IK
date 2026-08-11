using System.Data;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class DepartmentManagerService(
    HumanResourcesDbContext dbContext,
    AuditLogService auditLogService,
    EmployeeResponsibilityRoleService responsibilityRoleService)
{
    public async Task SaveInitialDepartmentAsync(
        string departmentName,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (await dbContext.Departments.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "İlk kurulum departmanı yalnızca departman tablosu boşken oluşturulabilir.");
        }

        var department = new Department
        {
            DepartmentName = departmentName.Trim()
        };
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.AppendSystemAsync(
            AuditActionType.DepartmentCreated,
            nameof(Department),
            department.DepartmentId.ToString(),
            SystemActorKeys.InitialConfiguration,
            $"DepartmentName={department.DepartmentName}; InitialConfiguration=true",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveDepartmentAsync(
        int? departmentId,
        string departmentName,
        int? parentDepartmentId,
        int? managerEmployeeId,
        int actorEmployeeId,
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

        var affectedEmployeeIds = await RecomputeDepartmentAsync(
            department.DepartmentId,
            cancellationToken);
        affectedEmployeeIds.UnionWith(await RecomputeChildManagersAsync(
            department.DepartmentId,
            cancellationToken));

        var reassignment = (LeaveTransferred: 0, LeaveBypassed: 0,
            CancellationTransferred: 0, CancellationBypassed: 0);
        if (priorManagerId.HasValue && priorManagerId != managerEmployeeId)
        {
            reassignment = await ReassignPendingManagerReviewsAsync(
                priorManagerId.Value,
                affectedEmployeeIds,
                cancellationToken);
        }

        if (priorManagerId != managerEmployeeId)
        {
            await responsibilityRoleService.ReconcileForEmployeeActorAsync(
                [priorManagerId ?? 0, managerEmployeeId ?? 0],
                actorEmployeeId,
                "DepartmentManagerChanged",
                cancellationToken);
        }

        var action = departmentId.HasValue
            ? AuditActionType.DepartmentUpdated
            : AuditActionType.DepartmentCreated;
        await auditLogService.AppendAsync(
            action,
            nameof(Department),
            department.DepartmentId.ToString(),
            actorEmployeeId,
            $"DepartmentName={department.DepartmentName}; ManagerChanged={priorManagerId != managerEmployeeId}");

        if (priorManagerId != managerEmployeeId)
        {
            await auditLogService.AppendAsync(
                AuditActionType.DepartmentManagerChanged,
                nameof(Department),
                department.DepartmentId.ToString(),
                actorEmployeeId,
                $"PreviousManagerEmployeeId={priorManagerId?.ToString() ?? "Yok"}; "
                + $"NewManagerEmployeeId={managerEmployeeId?.ToString() ?? "Yok"}; "
                + $"TransferredLeaveRequests={reassignment.LeaveTransferred}; "
                + $"BypassedLeaveRequests={reassignment.LeaveBypassed}; "
                + $"TransferredCancellationRequests={reassignment.CancellationTransferred}; "
                + $"BypassedCancellationRequests={reassignment.CancellationBypassed}");
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
        int applicationRoleId,
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

        if (managedDepartment is not null
            && applicationRoleId == ApplicationRoleDefaults.EmployeeRoleId)
        {
            throw new InvalidOperationException(
                "Departman yöneticisi veya aktif vekil Çalışan rolüne düşürülemez; rol görev sona erdiğinde sistem tarafından güncellenir.");
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

    private async Task<HashSet<int>> RecomputeDepartmentAsync(
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
        return employees.Select(employee => employee.EmployeeId).ToHashSet();
    }

    private async Task<HashSet<int>> RecomputeChildManagersAsync(
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
        var childDelegateIds = await dbContext.Departments
            .AsNoTracking()
            .Where(item =>
                item.ParentDepartmentId == departmentId
                && item.ActiveDelegateEmployeeId != null)
            .Select(item => item.ActiveDelegateEmployeeId!.Value)
            .ToListAsync(cancellationToken);
        childManagerIds.AddRange(childDelegateIds);
        childManagerIds = childManagerIds.Distinct().ToList();

        var childManagers = await dbContext.Employees
            .Where(item => childManagerIds.Contains(item.EmployeeId))
            .ToListAsync(cancellationToken);
        foreach (var childManager in childManagers)
        {
            childManager.ManagerId = parentManagerId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return childManagers.Select(employee => employee.EmployeeId).ToHashSet();
    }

    private async Task<(
        int LeaveTransferred,
        int LeaveBypassed,
        int CancellationTransferred,
        int CancellationBypassed)> ReassignPendingManagerReviewsAsync(
        int previousManagerEmployeeId,
        IReadOnlySet<int> affectedEmployeeIds,
        CancellationToken cancellationToken)
    {
        if (affectedEmployeeIds.Count == 0)
        {
            return (0, 0, 0, 0);
        }

        var currentManagerByEmployeeId = await dbContext.Employees
            .AsNoTracking()
            .Where(employee => affectedEmployeeIds.Contains(employee.EmployeeId))
            .ToDictionaryAsync(
                employee => employee.EmployeeId,
                employee => employee.ManagerId,
                cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var leaveRequests = await dbContext.LeaveRequests
            .Where(request =>
                request.CurrentStatus == LeaveRequestStatus.ManagerReview
                && request.ManagerApproverEmployeeId == previousManagerEmployeeId
                && affectedEmployeeIds.Contains(request.EmployeeId))
            .ToListAsync(cancellationToken);
        var leaveRequestIds = leaveRequests.Select(request => request.RequestId).ToList();
        var leaveApprovals = await dbContext.LeaveApprovals
            .Where(approval => leaveRequestIds.Contains(approval.RequestId))
            .ToListAsync(cancellationToken);
        var leaveApprovalByKey = leaveApprovals.ToDictionary(
            approval => (approval.RequestId, approval.ApproverRole));

        var leaveTransferred = 0;
        var leaveBypassed = 0;
        foreach (var request in leaveRequests)
        {
            var managerApprovalKey = (request.RequestId, LeaveApproverRole.Manager);
            if (!leaveApprovalByKey.TryGetValue(managerApprovalKey, out var managerApproval))
            {
                managerApproval = new LeaveApproval
                {
                    RequestId = request.RequestId,
                    ApproverRole = LeaveApproverRole.Manager,
                    Decision = LeaveApprovalDecision.Pending,
                    CreatedAt = now
                };
                dbContext.LeaveApprovals.Add(managerApproval);
                leaveApprovalByKey.Add(managerApprovalKey, managerApproval);
            }
            else if (managerApproval.Decision != LeaveApprovalDecision.Pending)
            {
                throw new InvalidOperationException(
                    "Yönetici onayı bekleyen izin talebinin karar kaydı beklemede değil.");
            }

            var currentManagerId = currentManagerByEmployeeId[request.EmployeeId];
            request.ManagerApproverEmployeeId = currentManagerId;
            request.UpdatedAt = now;
            managerApproval.ApproverEmployeeId = currentManagerId;
            if (currentManagerId.HasValue)
            {
                leaveTransferred++;
                continue;
            }

            managerApproval.Decision = LeaveApprovalDecision.Approved;
            managerApproval.DecisionDate = now;
            managerApproval.Comment =
                "Departman yöneticisi değişikliği sonrasında üst yönetici bulunmadığı için yönetici onayı uygulanmadı.";
            request.CurrentStatus = LeaveRequestStatus.HumanResourcesReview;
            var humanResourcesApprovalKey =
                (request.RequestId, LeaveApproverRole.HumanResources);
            if (leaveApprovalByKey.TryGetValue(
                    humanResourcesApprovalKey,
                    out var existingHumanResourcesApproval)
                && existingHumanResourcesApproval.Decision
                != LeaveApprovalDecision.Pending)
            {
                throw new InvalidOperationException(
                    "Yönetici onayı bekleyen izin talebinin İK karar kaydı terminal durumda.");
            }

            if (existingHumanResourcesApproval is null)
            {
                var humanResourcesApproval = new LeaveApproval
                {
                    RequestId = request.RequestId,
                    ApproverRole = LeaveApproverRole.HumanResources,
                    Decision = LeaveApprovalDecision.Pending,
                    CreatedAt = now
                };
                dbContext.LeaveApprovals.Add(humanResourcesApproval);
                leaveApprovalByKey.Add(humanResourcesApprovalKey, humanResourcesApproval);
            }

            leaveBypassed++;
        }

        var cancellationRequests = await dbContext.LeaveCancellationRequests
            .Where(request =>
                request.CurrentStatus == LeaveRequestStatus.ManagerReview
                && request.ManagerApproverEmployeeId == previousManagerEmployeeId
                && affectedEmployeeIds.Contains(request.LeaveRequest.EmployeeId))
            .Select(request => new
            {
                Request = request,
                EmployeeId = request.LeaveRequest.EmployeeId
            })
            .ToListAsync(cancellationToken);
        var cancellationRequestIds = cancellationRequests
            .Select(item => item.Request.CancellationRequestId)
            .ToList();
        var cancellationApprovals = await dbContext.LeaveCancellationApprovals
            .Where(approval =>
                cancellationRequestIds.Contains(approval.CancellationRequestId))
            .ToListAsync(cancellationToken);
        var cancellationApprovalByKey = cancellationApprovals.ToDictionary(
            approval => (approval.CancellationRequestId, approval.ApproverRole));

        var cancellationTransferred = 0;
        var cancellationBypassed = 0;
        foreach (var item in cancellationRequests)
        {
            var request = item.Request;
            var managerApprovalKey =
                (request.CancellationRequestId, LeaveApproverRole.Manager);
            if (!cancellationApprovalByKey.TryGetValue(
                    managerApprovalKey,
                    out var managerApproval))
            {
                managerApproval = new LeaveCancellationApproval
                {
                    CancellationRequestId = request.CancellationRequestId,
                    ApproverRole = LeaveApproverRole.Manager,
                    Decision = LeaveApprovalDecision.Pending,
                    CreatedAt = now
                };
                dbContext.LeaveCancellationApprovals.Add(managerApproval);
                cancellationApprovalByKey.Add(managerApprovalKey, managerApproval);
            }
            else if (managerApproval.Decision != LeaveApprovalDecision.Pending)
            {
                throw new InvalidOperationException(
                    "Yönetici onayı bekleyen izin iptal talebinin karar kaydı beklemede değil.");
            }

            var currentManagerId = currentManagerByEmployeeId[item.EmployeeId];
            request.ManagerApproverEmployeeId = currentManagerId;
            request.UpdatedAt = now;
            managerApproval.ApproverEmployeeId = currentManagerId;
            if (currentManagerId.HasValue)
            {
                cancellationTransferred++;
                continue;
            }

            managerApproval.Decision = LeaveApprovalDecision.Approved;
            managerApproval.DecisionDate = now;
            managerApproval.Comment =
                "Departman yöneticisi değişikliği sonrasında üst yönetici bulunmadığı için yönetici onayı uygulanmadı.";
            request.CurrentStatus = LeaveRequestStatus.HumanResourcesReview;
            var humanResourcesApprovalKey =
                (request.CancellationRequestId, LeaveApproverRole.HumanResources);
            if (cancellationApprovalByKey.TryGetValue(
                    humanResourcesApprovalKey,
                    out var existingHumanResourcesApproval)
                && existingHumanResourcesApproval.Decision
                != LeaveApprovalDecision.Pending)
            {
                throw new InvalidOperationException(
                    "Yönetici onayı bekleyen izin iptal talebinin İK karar kaydı terminal durumda.");
            }

            if (existingHumanResourcesApproval is null)
            {
                var humanResourcesApproval = new LeaveCancellationApproval
                {
                    CancellationRequestId = request.CancellationRequestId,
                    ApproverRole = LeaveApproverRole.HumanResources,
                    Decision = LeaveApprovalDecision.Pending,
                    CreatedAt = now
                };
                dbContext.LeaveCancellationApprovals.Add(humanResourcesApproval);
                cancellationApprovalByKey.Add(
                    humanResourcesApprovalKey,
                    humanResourcesApproval);
            }

            cancellationBypassed++;
        }

        return (
            leaveTransferred,
            leaveBypassed,
            cancellationTransferred,
            cancellationBypassed);
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
