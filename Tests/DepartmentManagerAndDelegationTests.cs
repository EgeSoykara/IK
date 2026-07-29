using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace IK.Web.Tests;

public sealed class DepartmentManagerAndDelegationTests
{
    [Fact]
    public void ManagerAuthorityBackfill_FailsClosedBeforeClearingLegacyTopology()
    {
        var repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var migration = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Migrations",
            "20260728144637_BackfillDepartmentManagerAuthority.cs"));

        Assert.Contains("candidate.DepartmentId <> employee.DepartmentId", migration);
        Assert.Contains("candidate.Status <> 1", migration);
        Assert.Contains("DECLARE @DepartmentCycleFound bit = 0", migration);
        Assert.Contains("WHERE HasCycle = 1", migration);
        Assert.Contains("childDepartment.ManagerEmployeeId IS NOT NULL", migration);
        Assert.Contains("parentDepartment.ManagerEmployeeId IS NULL", migration);
        Assert.True(
            migration.IndexOf("THROW 51004", StringComparison.Ordinal)
            < migration.IndexOf("UPDATE Employees", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DepartmentManagerChange_RecomputesEmployeesAndChildManager()
    {
        await using var db = CreateDbContext();
        db.Departments.AddRange(
            new Department { DepartmentId = 1, DepartmentName = "Üst" },
            new Department { DepartmentId = 2, DepartmentName = "Alt", ParentDepartmentId = 1 });
        db.Employees.AddRange(
            Employee(1, 1, "Üst Yönetici"),
            Employee(2, 1, "Üst Çalışan"),
            Employee(3, 2, "Alt Yönetici"),
            Employee(4, 2, "Alt Çalışan"));
        await db.SaveChangesAsync();

        var service = new DepartmentManagerService(db, new AuditLogService(db));
        await service.SaveDepartmentAsync(1, "Üst", null, 1, "admin");
        await service.SaveDepartmentAsync(2, "Alt", 1, 3, "admin");

        Assert.Null((await db.Employees.FindAsync(1))!.ManagerId);
        Assert.Equal(1, (await db.Employees.FindAsync(2))!.ManagerId);
        Assert.Equal(1, (await db.Employees.FindAsync(3))!.ManagerId);
        Assert.Equal(3, (await db.Employees.FindAsync(4))!.ManagerId);
    }

    [Fact]
    public async Task ChildManager_RequiresConfiguredParentManager()
    {
        await using var db = CreateDbContext();
        db.Departments.AddRange(
            new Department { DepartmentId = 1, DepartmentName = "Üst" },
            new Department { DepartmentId = 2, DepartmentName = "Alt", ParentDepartmentId = 1 });
        db.Employees.Add(Employee(3, 2, "Alt Yönetici"));
        await db.SaveChangesAsync();

        var service = new DepartmentManagerService(db, new AuditLogService(db));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveDepartmentAsync(2, "Alt", 1, 3, "admin"));

        Assert.Contains("üst departmanın yöneticisi", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParentManager_CannotBeClearedWhileManagedChildExists()
    {
        await using var db = CreateDbContext();
        db.Departments.AddRange(
            new Department { DepartmentId = 1, DepartmentName = "Üst", ManagerEmployeeId = 1 },
            new Department { DepartmentId = 2, DepartmentName = "Alt", ParentDepartmentId = 1, ManagerEmployeeId = 3 });
        db.Employees.AddRange(
            Employee(1, 1, "Üst Yönetici"),
            Employee(3, 2, "Alt Yönetici", 1));
        await db.SaveChangesAsync();

        var service = new DepartmentManagerService(db, new AuditLogService(db));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveDepartmentAsync(1, "Üst", null, null, "admin"));

        Assert.Contains("alt departmanlarda yönetici", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, (await db.Departments.FindAsync(1))!.ManagerEmployeeId);
    }

    [Fact]
    public async Task AssignedDelegate_CannotBeMovedOrMadePassiveBeforeLeaveEnds()
    {
        await using var db = CreateDbContext();
        db.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon",
            ManagerEmployeeId = 1
        });
        db.Employees.AddRange(
            Employee(1, 1, "Yönetici"),
            Employee(2, 1, "Vekil", 1));
        db.LeaveTypes.Add(new LeaveType { LeaveTypeId = 1, Name = "Yıllık", AnnualQuota = 20 });
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = 1,
            LeaveTypeId = 1,
            StartDate = DateTime.Today.AddDays(2),
            EndDate = DateTime.Today.AddDays(4),
            RequestedDays = 3,
            Reason = "İzin",
            CurrentStatus = LeaveRequestStatus.HumanResourcesReview,
            DelegateEmployeeId = 2
        });
        await db.SaveChangesAsync();

        var service = new DepartmentManagerService(db, new AuditLogService(db));
        var delegateEmployee = (await db.Employees.FindAsync(2))!;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EnsureEmployeeCanBeUpdatedAsync(
                delegateEmployee,
                1,
                EmploymentStatus.Passive));

        Assert.Contains("vekil olarak atanmış", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApprovedManagerLeave_DelegatesAndRestoresReportsAndPendingApprovals()
    {
        await using var db = CreateDbContext();
        var today = new DateOnly(2026, 7, 28);
        db.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon",
            ManagerEmployeeId = 1
        });
        db.Employees.AddRange(
            Employee(1, 1, "Yönetici"),
            Employee(2, 1, "Vekil", 1),
            Employee(3, 1, "Çalışan", 1));
        db.LeaveTypes.Add(new LeaveType { LeaveTypeId = 1, Name = "Yıllık", AnnualQuota = 20 });
        db.LeaveRequests.AddRange(
            new LeaveRequest
            {
                RequestId = 10,
                EmployeeId = 1,
                LeaveTypeId = 1,
                StartDate = today.ToDateTime(TimeOnly.MinValue),
                EndDate = today.ToDateTime(TimeOnly.MinValue),
                RequestedDays = 1,
                Reason = "İzin",
                CurrentStatus = LeaveRequestStatus.Approved,
                DelegateEmployeeId = 2
            },
            new LeaveRequest
            {
                RequestId = 11,
                EmployeeId = 3,
                LeaveTypeId = 1,
                StartDate = today.AddDays(2).ToDateTime(TimeOnly.MinValue),
                EndDate = today.AddDays(2).ToDateTime(TimeOnly.MinValue),
                RequestedDays = 1,
                Reason = "İzin",
                CurrentStatus = LeaveRequestStatus.ManagerReview,
                ManagerApproverEmployeeId = 1
            });
        await db.SaveChangesAsync();

        var service = new ManagerDelegationService(db, new AuditLogService(db), TimeProvider.System);
        await service.ReconcileAsync(today);

        Assert.Equal(2, (await db.Employees.FindAsync(3))!.ManagerId);
        Assert.Equal(2, (await db.LeaveRequests.FindAsync(11))!.ManagerApproverEmployeeId);
        Assert.Equal(2, (await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.Null((await db.ManagerDelegations.SingleAsync()).RestoredAt);

        await service.ReconcileAsync(today.AddDays(1));

        Assert.Equal(1, (await db.Employees.FindAsync(3))!.ManagerId);
        Assert.Equal(1, (await db.LeaveRequests.FindAsync(11))!.ManagerApproverEmployeeId);
        Assert.Null((await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.NotNull((await db.ManagerDelegations.SingleAsync()).RestoredAt);
    }

    [Fact]
    public async Task ApprovedChildDepartmentManagerLeave_ReportsDelegateToUpperDepartmentManager()
    {
        await using var db = CreateDbContext();
        var today = new DateOnly(2026, 7, 28);
        db.Departments.AddRange(
            new Department
            {
                DepartmentId = 1,
                DepartmentName = "Üst Departman",
                ManagerEmployeeId = 10
            },
            new Department
            {
                DepartmentId = 2,
                DepartmentName = "Alt Departman",
                ParentDepartmentId = 1,
                ManagerEmployeeId = 1
            });
        db.Employees.AddRange(
            Employee(10, 1, "Üst Yönetici"),
            Employee(1, 2, "Alt Yönetici", 10),
            Employee(2, 2, "Vekil", 1),
            Employee(3, 2, "Çalışan", 1));
        db.LeaveTypes.Add(new LeaveType { LeaveTypeId = 1, Name = "Yıllık", AnnualQuota = 20 });
        db.LeaveRequests.Add(new LeaveRequest
        {
            RequestId = 10,
            EmployeeId = 1,
            LeaveTypeId = 1,
            StartDate = today.ToDateTime(TimeOnly.MinValue),
            EndDate = today.ToDateTime(TimeOnly.MinValue),
            RequestedDays = 1,
            Reason = "İzin",
            CurrentStatus = LeaveRequestStatus.Approved,
            DelegateEmployeeId = 2
        });
        await db.SaveChangesAsync();

        var service = new ManagerDelegationService(db, new AuditLogService(db), TimeProvider.System);
        await service.ReconcileAsync(today);

        Assert.Equal(2, (await db.Departments.FindAsync(2))!.ActiveDelegateEmployeeId);
        Assert.Equal(10, (await db.Employees.FindAsync(2))!.ManagerId);
        Assert.Equal(2, (await db.Employees.FindAsync(3))!.ManagerId);
    }

    [Fact]
    public async Task ActiveDelegate_CanTransferWithoutTakingLeave()
    {
        await using var db = CreateDbContext();
        var today = DateOnly.FromDateTime(DateTime.Today);
        db.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon",
            ManagerEmployeeId = 1,
            ActiveDelegateEmployeeId = 2
        });
        db.Employees.AddRange(
            Employee(1, 1, "Ana Yönetici"),
            Employee(2, 1, "Mevcut Vekil"),
            Employee(3, 1, "Yeni Vekil"),
            Employee(4, 1, "Çalışan", 2));
        db.LeaveTypes.Add(new LeaveType { LeaveTypeId = 1, Name = "Yıllık", AnnualQuota = 20 });
        db.LeaveRequests.Add(new LeaveRequest
        {
            RequestId = 20,
            EmployeeId = 1,
            LeaveTypeId = 1,
            StartDate = today.ToDateTime(TimeOnly.MinValue),
            EndDate = today.AddDays(5).ToDateTime(TimeOnly.MinValue),
            RequestedDays = 4,
            Reason = "İzin",
            CurrentStatus = LeaveRequestStatus.Approved,
            DelegateEmployeeId = 2
        });
        db.ManagerDelegations.Add(new ManagerDelegation
        {
            LeaveRequestId = 20,
            DepartmentId = 1,
            ManagerEmployeeId = 1,
            DelegateEmployeeId = 2,
            StartDate = today,
            EndDate = today.AddDays(5),
            ActivatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new ManagerDelegationService(
            db,
            new AuditLogService(db),
            TimeProvider.System);
        await service.TransferActiveDelegationAsync(1, 2, 3, "delegate-user");

        Assert.Equal(3, (await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.Equal(3, (await db.Employees.FindAsync(2))!.ManagerId);
        Assert.Equal(3, (await db.Employees.FindAsync(4))!.ManagerId);
        Assert.Null((await db.Employees.FindAsync(3))!.ManagerId);
        var child = await db.ManagerDelegations
            .SingleAsync(item => item.ManagerEmployeeId == 2);
        Assert.NotNull(child.ParentManagerDelegationId);
        Assert.Null(child.LeaveRequestId);
        Assert.Contains(
            await db.AuditLogs.ToListAsync(),
            item => item.ActionType == AuditActionType.ManagerDelegationTransferred
                    && item.UserId == "delegate-user");

        var transferBackContext = await service.GetTransferContextAsync(3);
        Assert.NotNull(transferBackContext);
        Assert.Contains(
            transferBackContext.Candidates,
            candidate => candidate.EmployeeId == 2);

        await service.TransferActiveDelegationAsync(1, 3, 2, "second-delegate-user");

        Assert.Equal(2, (await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.Equal(2, (await db.Employees.FindAsync(3))!.ManagerId);
        Assert.Equal(2, (await db.Employees.FindAsync(4))!.ManagerId);
        Assert.Null((await db.Employees.FindAsync(2))!.ManagerId);
        Assert.NotNull((await db.ManagerDelegations.FindAsync(child.ManagerDelegationId))!.RestoredAt);
        Assert.Equal(2, await db.ManagerDelegations.CountAsync());
        Assert.Contains(
            await db.AuditLogs.ToListAsync(),
            item => item.ActionType == AuditActionType.ManagerDelegationTransferred
                    && item.UserId == "second-delegate-user"
                    && item.Details == "Aktif vekâlet önceki aktif vekile geri devredildi.");
    }

    [Fact]
    public async Task TransferredActiveDelegate_CanAccessSeeAndApproveOnlyReassignedRequest()
    {
        await using var db = CreateDbContext();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var approvalDay = today.DayOfWeek switch
        {
            DayOfWeek.Saturday => today.AddDays(2),
            DayOfWeek.Sunday => today.AddDays(1),
            _ => today
        };
        db.Departments.AddRange(
            new Department
            {
                DepartmentId = 1,
                DepartmentName = "Operasyon",
                ManagerEmployeeId = 1,
                ActiveDelegateEmployeeId = 2
            },
            new Department
            {
                DepartmentId = 2,
                DepartmentName = "Finans",
                ManagerEmployeeId = 5
            });
        db.Employees.AddRange(
            Employee(1, 1, "Ana Yönetici"),
            Employee(2, 1, "Mevcut Vekil"),
            Employee(3, 1, "Yeni Vekil"),
            Employee(4, 1, "Operasyon Çalışanı", 2),
            Employee(5, 2, "Finans Yöneticisi"),
            Employee(6, 2, "Finans Çalışanı", 5));
        db.LeaveTypes.Add(new LeaveType { LeaveTypeId = 1, Name = "Yıllık", AnnualQuota = 20 });
        db.LeaveRequests.AddRange(
            new LeaveRequest
            {
                RequestId = 20,
                EmployeeId = 1,
                LeaveTypeId = 1,
                StartDate = today.ToDateTime(TimeOnly.MinValue),
                EndDate = today.AddDays(5).ToDateTime(TimeOnly.MinValue),
                RequestedDays = 4,
                Reason = "Ana yönetici izni",
                CurrentStatus = LeaveRequestStatus.Approved,
                DelegateEmployeeId = 2
            },
            new LeaveRequest
            {
                RequestId = 21,
                EmployeeId = 4,
                LeaveTypeId = 1,
                StartDate = approvalDay.ToDateTime(TimeOnly.MinValue),
                EndDate = approvalDay.ToDateTime(TimeOnly.MinValue),
                RequestedDays = 1,
                Reason = "Operasyon talebi",
                CurrentStatus = LeaveRequestStatus.ManagerReview,
                ManagerApproverEmployeeId = 2
            },
            new LeaveRequest
            {
                RequestId = 22,
                EmployeeId = 6,
                LeaveTypeId = 1,
                StartDate = approvalDay.ToDateTime(TimeOnly.MinValue),
                EndDate = approvalDay.ToDateTime(TimeOnly.MinValue),
                RequestedDays = 1,
                Reason = "Finans talebi",
                CurrentStatus = LeaveRequestStatus.ManagerReview,
                ManagerApproverEmployeeId = 5
            });
        db.ManagerDelegations.Add(new ManagerDelegation
        {
            LeaveRequestId = 20,
            DepartmentId = 1,
            ManagerEmployeeId = 1,
            DelegateEmployeeId = 2,
            StartDate = today,
            EndDate = today.AddDays(5),
            ActivatedAt = DateTimeOffset.UtcNow
        });
        db.LeaveApprovals.AddRange(
            new LeaveApproval
            {
                RequestId = 21,
                ApproverRole = LeaveApproverRole.Manager,
                Decision = LeaveApprovalDecision.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new LeaveApproval
            {
                RequestId = 22,
                ApproverRole = LeaveApproverRole.Manager,
                Decision = LeaveApprovalDecision.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync();

        var auditLogService = new AuditLogService(db);
        var delegationService = new ManagerDelegationService(
            db,
            auditLogService,
            TimeProvider.System);
        await delegationService.TransferActiveDelegationAsync(1, 2, 3, "delegate-user");

        var newDelegate = Principal(3);
        var accessService = new PageAccessService(
            TestHumanResourcesDbContextFactory.From(db));
        Assert.True(await accessService.CanAccessLeaveApprovalsAsync(newDelegate));
        Assert.False(await accessService.CanAccessLeaveApprovalsAsync(Principal(2)));
        Assert.Equal(
            [21],
            await db.LeaveApprovals
                .AsNoTracking()
                .VisibleTo(newDelegate)
                .Select(approval => approval.RequestId)
                .ToListAsync());

        var leaveRequestService = new LeaveRequestService(
            db,
            new LeaveDayCalculator(),
            new PublicHolidayCalendar(db),
            auditLogService,
            delegationService,
            TimeProvider.System,
            NullLogger<LeaveRequestService>.Instance);
        await leaveRequestService.ManagerDecisionAsync(
            21,
            managerEmployeeId: 3,
            approve: true,
            comment: "Uygun",
            actorUserId: "new-delegate");

        Assert.Equal(
            LeaveRequestStatus.HumanResourcesReview,
            (await db.LeaveRequests.FindAsync(21))!.CurrentStatus);
        Assert.Equal(
            LeaveRequestStatus.ManagerReview,
            (await db.LeaveRequests.FindAsync(22))!.CurrentStatus);
        Assert.Contains(
            await db.LeaveApprovals.Where(approval => approval.RequestId == 21).ToListAsync(),
            approval => approval.ApproverRole == LeaveApproverRole.HumanResources
                        && approval.Decision == LeaveApprovalDecision.Pending);
    }

    [Fact]
    public async Task NestedDelegateLeave_UnwindsBeforePrimaryManagerReturns()
    {
        await using var db = CreateDbContext();
        var today = DateOnly.FromDateTime(DateTime.Today);
        db.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon",
            ManagerEmployeeId = 1
        });
        db.Employees.AddRange(
            Employee(1, 1, "Ana Yönetici"),
            Employee(2, 1, "Birinci Vekil", 1),
            Employee(3, 1, "İkinci Vekil", 1),
            Employee(4, 1, "Çalışan", 1));
        db.LeaveTypes.Add(new LeaveType { LeaveTypeId = 1, Name = "Yıllık", AnnualQuota = 20 });
        db.LeaveRequests.AddRange(
            new LeaveRequest
            {
                RequestId = 30,
                EmployeeId = 1,
                LeaveTypeId = 1,
                StartDate = today.ToDateTime(TimeOnly.MinValue),
                EndDate = today.AddDays(5).ToDateTime(TimeOnly.MinValue),
                RequestedDays = 4,
                Reason = "Ana yönetici izni",
                CurrentStatus = LeaveRequestStatus.Approved,
                DelegateEmployeeId = 2
            },
            new LeaveRequest
            {
                RequestId = 31,
                EmployeeId = 2,
                LeaveTypeId = 1,
                StartDate = today.ToDateTime(TimeOnly.MinValue),
                EndDate = today.ToDateTime(TimeOnly.MinValue),
                RequestedDays = 1,
                Reason = "Vekil izni",
                CurrentStatus = LeaveRequestStatus.Approved,
                DelegateEmployeeId = 3
            });
        await db.SaveChangesAsync();

        var service = new ManagerDelegationService(
            db,
            new AuditLogService(db),
            TimeProvider.System);
        await service.ReconcileAsync(today);

        Assert.Equal(3, (await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.Equal(2, await db.ManagerDelegations.CountAsync());

        await service.ReconcileAsync(today.AddDays(1));

        Assert.Equal(2, (await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.Equal(2, (await db.Employees.FindAsync(4))!.ManagerId);
        var nested = await db.ManagerDelegations.SingleAsync(item => item.LeaveRequestId == 31);
        Assert.NotNull(nested.RestoredAt);

        await service.ReconcileAsync(today.AddDays(6));

        Assert.Null((await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.Equal(1, (await db.Employees.FindAsync(4))!.ManagerId);
    }

    private static Employee Employee(int id, int departmentId, string name, int? managerId = null)
    {
        var parts = name.Split(' ', 2);
        return new Employee
        {
            EmployeeId = id,
            DepartmentId = departmentId,
            ManagerId = managerId,
            FirstName = parts[0],
            LastName = parts.Length == 2 ? parts[1] : "Çalışan",
            SicilNo = $"S{id}",
            KktcKimlikNo = id.ToString("D10"),
            Status = EmploymentStatus.Active
        };
    }

    private static ClaimsPrincipal Principal(int employeeId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, $"employee-{employeeId}"),
                new Claim(UserClaimTypes.EmployeeId, employeeId.ToString())
            ],
            "Test");
        return new ClaimsPrincipal(identity);
    }

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new HumanResourcesDbContext(options);
    }
}
