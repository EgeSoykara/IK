using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace IK.Web.Tests;

public sealed class DepartmentManagerAndDelegationTests
{
    [Fact]
    public async Task InitialDepartment_IsSingleUseAndAuditedByConfigurationActor()
    {
        await using var db = CreateDbContext();
        var service = CreateDepartmentManagerService(db);

        await service.SaveInitialDepartmentAsync("İlk Departman");

        var department = Assert.Single(db.Departments);
        Assert.Equal("İlk Departman", department.DepartmentName);
        var audit = Assert.Single(db.AuditLogs);
        Assert.Equal(SystemActorKeys.InitialConfiguration, audit.SystemActorKey);
        Assert.Null(audit.ActorEmployeeId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveInitialDepartmentAsync("İkinci Departman"));
    }

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

        var service = CreateDepartmentManagerService(db);
        await service.SaveDepartmentAsync(1, "Üst", null, 1, 1);
        await service.SaveDepartmentAsync(2, "Alt", 1, 3, 1);

        Assert.Null((await db.Employees.FindAsync(1))!.ManagerId);
        Assert.Equal(1, (await db.Employees.FindAsync(2))!.ManagerId);
        Assert.Equal(1, (await db.Employees.FindAsync(3))!.ManagerId);
        Assert.Equal(3, (await db.Employees.FindAsync(4))!.ManagerId);
        Assert.Equal(
            ApplicationRoleDefaults.ManagerRoleId,
            (await db.Employees.FindAsync(1))!.ApplicationRoleId);
        Assert.Equal(
            ApplicationRoleDefaults.ManagerRoleId,
            (await db.Employees.FindAsync(3))!.ApplicationRoleId);

        await service.SaveDepartmentAsync(1, "Üst", null, 2, 1);

        Assert.Equal(
            ApplicationRoleDefaults.EmployeeRoleId,
            (await db.Employees.FindAsync(1))!.ApplicationRoleId);
        Assert.Equal(
            ApplicationRoleDefaults.ManagerRoleId,
            (await db.Employees.FindAsync(2))!.ApplicationRoleId);
    }

    [Fact]
    public async Task DepartmentManagerChange_ReassignsAffectedPendingWorkflowsWithoutSelfApproval()
    {
        await using var db = CreateDbContext();
        db.Departments.AddRange(
            new Department { DepartmentId = 1, DepartmentName = "Üst" },
            new Department { DepartmentId = 2, DepartmentName = "Alt", ParentDepartmentId = 1 });
        db.Employees.AddRange(
            Employee(1, 1, "Eski Yönetici"),
            Employee(2, 1, "Yeni Yönetici"),
            Employee(3, 2, "Alt Yönetici"),
            Employee(4, 1, "Çalışan"),
            Employee(5, 2, "Alt Vekil"));
        await db.SaveChangesAsync();

        var service = CreateDepartmentManagerService(db);
        await service.SaveDepartmentAsync(1, "Üst", null, 1, 1);
        await service.SaveDepartmentAsync(2, "Alt", 1, 3, 1);
        var childDepartment = (await db.Departments.FindAsync(2))!;
        childDepartment.ActiveDelegateEmployeeId = 5;
        (await db.Employees.FindAsync(5))!.ManagerId = 1;
        await db.SaveChangesAsync();

        var today = DateTime.Today;
        db.LeaveRequests.AddRange(
            PendingLeave(10, 4, 1, today),
            PendingLeave(11, 3, 1, today.AddDays(1)),
            PendingLeave(12, 2, 1, today.AddDays(2)),
            PendingLeave(14, 5, 1, today.AddDays(3)),
            new LeaveRequest
            {
                RequestId = 13,
                EmployeeId = 4,
                Category = LeaveRequestCategory.AnnualLeave,
                StartDate = today.AddDays(3),
                EndDate = today.AddDays(3),
                RequestedDays = 1,
                Reason = "İK bekleyen",
                CurrentStatus = LeaveRequestStatus.HumanResourcesReview,
                ManagerApproverEmployeeId = 1
            },
            ApprovedLeave(20, 4, 1, today.AddDays(4)),
            ApprovedLeave(21, 2, 1, today.AddDays(5)),
            ApprovedLeave(22, 5, 1, today.AddDays(6)));
        db.LeaveApprovals.AddRange(
            PendingManagerApproval(10, 1),
            PendingManagerApproval(11, 1),
            PendingManagerApproval(12, 1),
            PendingManagerApproval(14, 1));
        db.LeaveCancellationRequests.AddRange(
            PendingCancellation(30, 20, 1, today.AddDays(4)),
            PendingCancellation(31, 21, 1, today.AddDays(5)),
            PendingCancellation(32, 22, 1, today.AddDays(6)));
        db.LeaveCancellationApprovals.AddRange(
            PendingCancellationApproval(30, 1),
            PendingCancellationApproval(31, 1),
            PendingCancellationApproval(32, 1));
        await db.SaveChangesAsync();

        await service.SaveDepartmentAsync(1, "Üst", null, 2, 1);

        Assert.Equal(2, (await db.LeaveRequests.FindAsync(10))!.ManagerApproverEmployeeId);
        Assert.Equal(2, (await db.LeaveRequests.FindAsync(11))!.ManagerApproverEmployeeId);
        Assert.Equal(2, (await db.Employees.FindAsync(5))!.ManagerId);
        Assert.Equal(2, (await db.LeaveRequests.FindAsync(14))!.ManagerApproverEmployeeId);
        Assert.Equal(
            2,
            (await db.LeaveApprovals.SingleAsync(item =>
                item.RequestId == 10 && item.ApproverRole == LeaveApproverRole.Manager))
            .ApproverEmployeeId);

        var promotedManagerRequest = (await db.LeaveRequests.FindAsync(12))!;
        Assert.Equal(LeaveRequestStatus.HumanResourcesReview, promotedManagerRequest.CurrentStatus);
        Assert.Null(promotedManagerRequest.ManagerApproverEmployeeId);
        var promotedManagerDecision = await db.LeaveApprovals.SingleAsync(item =>
            item.RequestId == 12 && item.ApproverRole == LeaveApproverRole.Manager);
        Assert.Equal(LeaveApprovalDecision.Approved, promotedManagerDecision.Decision);
        Assert.Null(promotedManagerDecision.ApproverEmployeeId);
        Assert.Contains(
            await db.LeaveApprovals.ToListAsync(),
            item => item.RequestId == 12
                    && item.ApproverRole == LeaveApproverRole.HumanResources
                    && item.Decision == LeaveApprovalDecision.Pending);

        Assert.Equal(
            LeaveRequestStatus.HumanResourcesReview,
            (await db.LeaveRequests.FindAsync(13))!.CurrentStatus);
        Assert.Equal(1, (await db.LeaveRequests.FindAsync(13))!.ManagerApproverEmployeeId);

        Assert.Equal(
            2,
            (await db.LeaveCancellationRequests.FindAsync(30))!.ManagerApproverEmployeeId);
        Assert.Equal(
            2,
            (await db.LeaveCancellationRequests.FindAsync(32))!.ManagerApproverEmployeeId);
        Assert.Equal(
            2,
            (await db.LeaveCancellationApprovals.SingleAsync(item =>
                item.CancellationRequestId == 30
                && item.ApproverRole == LeaveApproverRole.Manager))
            .ApproverEmployeeId);

        var promotedManagerCancellation =
            (await db.LeaveCancellationRequests.FindAsync(31))!;
        Assert.Equal(
            LeaveRequestStatus.HumanResourcesReview,
            promotedManagerCancellation.CurrentStatus);
        Assert.Null(promotedManagerCancellation.ManagerApproverEmployeeId);
        Assert.Contains(
            await db.LeaveCancellationApprovals.ToListAsync(),
            item => item.CancellationRequestId == 31
                    && item.ApproverRole == LeaveApproverRole.Manager
                    && item.Decision == LeaveApprovalDecision.Approved
                    && item.ApproverEmployeeId == null);
        Assert.Contains(
            await db.LeaveCancellationApprovals.ToListAsync(),
            item => item.CancellationRequestId == 31
                    && item.ApproverRole == LeaveApproverRole.HumanResources
                    && item.Decision == LeaveApprovalDecision.Pending);

        var audit = await db.AuditLogs
            .Where(item => item.ActionType == AuditActionType.DepartmentManagerChanged)
            .OrderByDescending(item => item.AuditLogId)
            .FirstAsync();
        Assert.Contains("TransferredLeaveRequests=3", audit.Details);
        Assert.Contains("BypassedLeaveRequests=1", audit.Details);
        Assert.Contains("TransferredCancellationRequests=2", audit.Details);
        Assert.Contains("BypassedCancellationRequests=1", audit.Details);
    }

    [Fact]
    public async Task DepartmentManagerChange_ReassignmentExecutesOnRelationalProvider()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation(
            "Latin1_General_100_BIN2",
            static (left, right) => string.CompareOrdinal(left, right));
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new HumanResourcesDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon"
        });
        await db.SaveChangesAsync();
        var createdAt = DateTimeOffset.UtcNow;
        var rowVersion = new byte[] { 1 };
        foreach (var employee in new[]
                 {
                     (Id: 1, FirstName: "Eski", LastName: "Yönetici"),
                     (Id: 2, FirstName: "Yeni", LastName: "Yönetici"),
                     (Id: 3, FirstName: "Test", LastName: "Çalışan")
                 })
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Employees
                    (EmployeeId, SicilNo, FirstName, LastName, Email, KKTC_KimlikNo,
                     DepartmentId, ApplicationRoleId, ManagerId, StartDate, StaffDate,
                     Gender, BloodGroup, Status, CreatedAt, UpdatedAt, RowVersion)
                VALUES
                    ({employee.Id}, {$"S{employee.Id}"}, {employee.FirstName},
                     {employee.LastName}, {$"employee{employee.Id}@example.test"},
                     {employee.Id.ToString("D10")}, 1,
                     {ApplicationRoleDefaults.EmployeeRoleId}, NULL, NULL, NULL,
                     NULL, NULL, {(int)EmploymentStatus.Active}, {createdAt},
                     {createdAt}, {rowVersion});
                """);
        }
        db.ChangeTracker.Clear();

        var service = CreateDepartmentManagerService(db);
        await service.SaveDepartmentAsync(1, "Operasyon", null, 1, 1);

        var today = DateTime.Today;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO LeaveRequests
                (RequestId, EmployeeId, Category, LeaveTypeId, StartDate, EndDate,
                 RequestedDays, Reason, CurrentStatus, IsRetrospective,
                 ManagerApproverEmployeeId, DelegateEmployeeId, CreatedAt, UpdatedAt,
                 RowVersion)
            VALUES
                (40, 3, {(int)LeaveRequestCategory.AnnualLeave}, NULL, {today}, {today},
                 {1m}, {"Bekleyen izin"}, {(int)LeaveRequestStatus.ManagerReview},
                 {false}, 1, NULL, {createdAt}, {createdAt}, {rowVersion}),
                (41, 3, {(int)LeaveRequestCategory.AnnualLeave}, NULL,
                 {today.AddDays(1)}, {today.AddDays(1)}, {1m}, {"Onaylı izin"},
                 {(int)LeaveRequestStatus.Approved}, {false}, 1, NULL, {createdAt},
                 {createdAt}, {rowVersion});
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO LeaveCancellationRequests
                (CancellationRequestId, LeaveRequestId, CancellationStartDate,
                 CancellationEndDate, RequestedRefundDays, Reason,
                 IsDirectCancellation, RequestedByEmployeeId, RequestedByDisplayName,
                 CurrentStatus, ManagerApproverEmployeeId, CreatedAt, UpdatedAt,
                 RowVersion)
            VALUES
                (42, 41, {today.AddDays(1)}, {today.AddDays(1)}, {1m},
                 {"Bekleyen iptal"}, {false}, NULL, NULL,
                 {(int)LeaveRequestStatus.ManagerReview}, 1, {createdAt}, {createdAt},
                 {rowVersion});
            """);
        db.LeaveApprovals.Add(PendingManagerApproval(40, 1));
        db.LeaveCancellationApprovals.Add(
            PendingCancellationApproval(42, 1));
        await db.SaveChangesAsync();

        await service.SaveDepartmentAsync(1, "Operasyon", null, 2, 1);

        Assert.Equal(2, (await db.LeaveRequests.FindAsync(40))!.ManagerApproverEmployeeId);
        Assert.Equal(
            2,
            (await db.LeaveApprovals.SingleAsync(item =>
                item.RequestId == 40 && item.ApproverRole == LeaveApproverRole.Manager))
            .ApproverEmployeeId);
        Assert.Equal(
            2,
            (await db.LeaveCancellationRequests.FindAsync(42))!.ManagerApproverEmployeeId);
        Assert.Equal(
            2,
            (await db.LeaveCancellationApprovals.SingleAsync(item =>
                item.CancellationRequestId == 42
                && item.ApproverRole == LeaveApproverRole.Manager))
            .ApproverEmployeeId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DepartmentManagerChange_RejectsContradictoryTerminalHumanResourcesDecision(
        bool cancellationWorkflow)
    {
        await using var db = CreateDbContext();
        db.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon"
        });
        db.Employees.AddRange(
            Employee(1, 1, "Eski Yönetici"),
            Employee(2, 1, "Yeni Yönetici"));
        await db.SaveChangesAsync();

        var service = CreateDepartmentManagerService(db);
        await service.SaveDepartmentAsync(1, "Operasyon", null, 1, 1);
        var today = DateTime.Today;
        if (cancellationWorkflow)
        {
            db.LeaveRequests.Add(ApprovedLeave(50, 2, 1, today));
            db.LeaveCancellationRequests.Add(
                PendingCancellation(51, 50, 1, today));
            db.LeaveCancellationApprovals.AddRange(
                PendingCancellationApproval(51, 1),
                new LeaveCancellationApproval
                {
                    CancellationRequestId = 51,
                    ApproverRole = LeaveApproverRole.HumanResources,
                    Decision = LeaveApprovalDecision.Approved,
                    DecisionDate = DateTimeOffset.UtcNow
                });
        }
        else
        {
            db.LeaveRequests.Add(PendingLeave(50, 2, 1, today));
            db.LeaveApprovals.AddRange(
                PendingManagerApproval(50, 1),
                new LeaveApproval
                {
                    RequestId = 50,
                    ApproverRole = LeaveApproverRole.HumanResources,
                    Decision = LeaveApprovalDecision.Rejected,
                    DecisionDate = DateTimeOffset.UtcNow
                });
        }

        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveDepartmentAsync(1, "Operasyon", null, 2, 1));

        Assert.Contains("İK karar kaydı terminal durumda", exception.Message);
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

        var service = CreateDepartmentManagerService(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveDepartmentAsync(2, "Alt", 1, 3, 1));

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

        var service = CreateDepartmentManagerService(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveDepartmentAsync(1, "Üst", null, null, 1));

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
            Category = LeaveRequestCategory.AnnualLeave,
            StartDate = DateTime.Today.AddDays(2),
            EndDate = DateTime.Today.AddDays(4),
            RequestedDays = 3,
            Reason = "İzin",
            CurrentStatus = LeaveRequestStatus.HumanResourcesReview,
            DelegateEmployeeId = 2
        });
        await db.SaveChangesAsync();

        var service = CreateDepartmentManagerService(db);
        var delegateEmployee = (await db.Employees.FindAsync(2))!;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EnsureEmployeeCanBeUpdatedAsync(
                delegateEmployee,
                1,
                EmploymentStatus.Passive,
                delegateEmployee.ApplicationRoleId));

        Assert.Contains("vekil olarak atanmış", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResponsibleEmployee_CannotBeManuallyAssignedEmployeeRole()
    {
        await using var db = CreateDbContext();
        db.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon",
            ManagerEmployeeId = 1
        });
        var manager = Employee(1, 1, "Yönetici");
        manager.ApplicationRoleId = ApplicationRoleDefaults.ManagerRoleId;
        db.Employees.Add(manager);
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateDepartmentManagerService(db).EnsureEmployeeCanBeUpdatedAsync(
                manager,
                manager.DepartmentId,
                manager.Status,
                ApplicationRoleDefaults.EmployeeRoleId));

        Assert.Contains("Çalışan rolüne düşürülemez", error.Message);
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
                Category = LeaveRequestCategory.AnnualLeave,
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
                Category = LeaveRequestCategory.AnnualLeave,
                StartDate = today.AddDays(2).ToDateTime(TimeOnly.MinValue),
                EndDate = today.AddDays(2).ToDateTime(TimeOnly.MinValue),
                RequestedDays = 1,
                Reason = "İzin",
                CurrentStatus = LeaveRequestStatus.ManagerReview,
                ManagerApproverEmployeeId = 1
            });
        await db.SaveChangesAsync();

        var service = CreateManagerDelegationService(db);
        await service.ReconcileAsync(today);

        Assert.Equal(2, (await db.Employees.FindAsync(3))!.ManagerId);
        Assert.Equal(2, (await db.LeaveRequests.FindAsync(11))!.ManagerApproverEmployeeId);
        Assert.Equal(2, (await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.Null((await db.ManagerDelegations.SingleAsync()).RestoredAt);
        Assert.Equal(
            ApplicationRoleDefaults.ManagerRoleId,
            (await db.Employees.FindAsync(1))!.ApplicationRoleId);
        Assert.Equal(
            ApplicationRoleDefaults.ManagerRoleId,
            (await db.Employees.FindAsync(2))!.ApplicationRoleId);

        await service.ReconcileAsync(today.AddDays(1));

        Assert.Equal(1, (await db.Employees.FindAsync(3))!.ManagerId);
        Assert.Equal(1, (await db.LeaveRequests.FindAsync(11))!.ManagerApproverEmployeeId);
        Assert.Null((await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.NotNull((await db.ManagerDelegations.SingleAsync()).RestoredAt);
        Assert.Equal(
            ApplicationRoleDefaults.ManagerRoleId,
            (await db.Employees.FindAsync(1))!.ApplicationRoleId);
        Assert.Equal(
            ApplicationRoleDefaults.EmployeeRoleId,
            (await db.Employees.FindAsync(2))!.ApplicationRoleId);
    }

    [Fact]
    public async Task ApprovedMiddleCancellation_PausesAndThenResumesManagerDelegation()
    {
        await using var db = CreateDbContext();
        var firstLeaveDay = new DateOnly(2026, 7, 28);
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
        db.LeaveRequests.Add(new LeaveRequest
        {
            RequestId = 10,
            EmployeeId = 1,
            Category = LeaveRequestCategory.AnnualLeave,
            StartDate = firstLeaveDay.ToDateTime(TimeOnly.MinValue),
            EndDate = firstLeaveDay.AddDays(2).ToDateTime(TimeOnly.MinValue),
            RequestedDays = 2,
            Reason = "Üç günlük dönem, orta gün iptal",
            CurrentStatus = LeaveRequestStatus.Approved,
            DelegateEmployeeId = 2
        });
        db.LeaveCancellationRequests.Add(new LeaveCancellationRequest
        {
            LeaveRequestId = 10,
            CancellationStartDate = firstLeaveDay.AddDays(1).ToDateTime(TimeOnly.MinValue),
            CancellationEndDate = firstLeaveDay.AddDays(1).ToDateTime(TimeOnly.MinValue),
            RequestedRefundDays = 1,
            Reason = "Orta gün işe dönüş",
            CurrentStatus = LeaveRequestStatus.Approved
        });
        await db.SaveChangesAsync();

        var service = CreateManagerDelegationService(db);
        await service.ReconcileAsync(firstLeaveDay);
        Assert.Equal(2, (await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);

        await service.ReconcileAsync(firstLeaveDay.AddDays(1));
        Assert.Null((await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.Single(await db.ManagerDelegations.Where(item => item.RestoredAt != null).ToListAsync());

        await service.ReconcileAsync(firstLeaveDay.AddDays(2));
        Assert.Equal(2, (await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.Equal(2, await db.ManagerDelegations.CountAsync());
        Assert.Single(await db.ManagerDelegations.Where(item => item.RestoredAt == null).ToListAsync());
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
            Category = LeaveRequestCategory.AnnualLeave,
            StartDate = today.ToDateTime(TimeOnly.MinValue),
            EndDate = today.ToDateTime(TimeOnly.MinValue),
            RequestedDays = 1,
            Reason = "İzin",
            CurrentStatus = LeaveRequestStatus.Approved,
            DelegateEmployeeId = 2
        });
        await db.SaveChangesAsync();

        var service = CreateManagerDelegationService(db);
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
        var manager = Employee(1, 1, "Ana Yönetici");
        manager.ApplicationRoleId = ApplicationRoleDefaults.ManagerRoleId;
        var existingDelegate = Employee(2, 1, "Mevcut Vekil");
        existingDelegate.ApplicationRoleId = ApplicationRoleDefaults.ManagerRoleId;
        db.Employees.AddRange(
            manager,
            existingDelegate,
            Employee(3, 1, "Yeni Vekil"),
            Employee(4, 1, "Çalışan", 2));
        db.LeaveTypes.Add(new LeaveType { LeaveTypeId = 1, Name = "Yıllık", AnnualQuota = 20 });
        db.LeaveRequests.Add(new LeaveRequest
        {
            RequestId = 20,
            EmployeeId = 1,
            Category = LeaveRequestCategory.AnnualLeave,
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

        var service = CreateManagerDelegationService(db);
        await service.TransferActiveDelegationAsync(1, 2, 3);

        Assert.Equal(3, (await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.Equal(3, (await db.Employees.FindAsync(2))!.ManagerId);
        Assert.Equal(3, (await db.Employees.FindAsync(4))!.ManagerId);
        Assert.Null((await db.Employees.FindAsync(3))!.ManagerId);
        Assert.Equal(
            ApplicationRoleDefaults.EmployeeRoleId,
            (await db.Employees.FindAsync(2))!.ApplicationRoleId);
        Assert.Equal(
            ApplicationRoleDefaults.ManagerRoleId,
            (await db.Employees.FindAsync(3))!.ApplicationRoleId);
        var child = await db.ManagerDelegations
            .SingleAsync(item => item.ManagerEmployeeId == 2);
        Assert.NotNull(child.ParentManagerDelegationId);
        Assert.Null(child.LeaveRequestId);
        Assert.Contains(
            await db.AuditLogs.ToListAsync(),
            item => item.ActionType == AuditActionType.ManagerDelegationTransferred
                    && item.ActorEmployeeId == 2);

        var transferBackContext = await service.GetTransferContextAsync(3);
        Assert.NotNull(transferBackContext);
        Assert.Contains(
            transferBackContext.Candidates,
            candidate => candidate.EmployeeId == 2);

        await service.TransferActiveDelegationAsync(1, 3, 2);

        Assert.Equal(2, (await db.Departments.FindAsync(1))!.ActiveDelegateEmployeeId);
        Assert.Equal(2, (await db.Employees.FindAsync(3))!.ManagerId);
        Assert.Equal(2, (await db.Employees.FindAsync(4))!.ManagerId);
        Assert.Null((await db.Employees.FindAsync(2))!.ManagerId);
        Assert.Equal(
            ApplicationRoleDefaults.ManagerRoleId,
            (await db.Employees.FindAsync(2))!.ApplicationRoleId);
        Assert.Equal(
            ApplicationRoleDefaults.EmployeeRoleId,
            (await db.Employees.FindAsync(3))!.ApplicationRoleId);
        Assert.NotNull((await db.ManagerDelegations.FindAsync(child.ManagerDelegationId))!.RestoredAt);
        Assert.Equal(2, await db.ManagerDelegations.CountAsync());
        Assert.Contains(
            await db.AuditLogs.ToListAsync(),
            item => item.ActionType == AuditActionType.ManagerDelegationTransferred
                    && item.ActorEmployeeId == 3
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
                Category = LeaveRequestCategory.AnnualLeave,
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
                Category = LeaveRequestCategory.AnnualLeave,
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
                Category = LeaveRequestCategory.AnnualLeave,
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
            new EmployeeResponsibilityRoleService(db, auditLogService),
            TimeProvider.System);
        await delegationService.TransferActiveDelegationAsync(1, 2, 3);

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
            new LeaveEntitlementService(),
            new PublicHolidayCalendar(TestHumanResourcesDbContextFactory.From(db)),
            auditLogService,
            delegationService,
            TimeProvider.System,
            NullLogger<LeaveRequestService>.Instance);
        await leaveRequestService.ManagerDecisionAsync(
            21,
            managerEmployeeId: 3,
            approve: true,
            comment: "Uygun",
            actorEmployeeId: 1);

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
                Category = LeaveRequestCategory.AnnualLeave,
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
                Category = LeaveRequestCategory.AnnualLeave,
                StartDate = today.ToDateTime(TimeOnly.MinValue),
                EndDate = today.ToDateTime(TimeOnly.MinValue),
                RequestedDays = 1,
                Reason = "Vekil izni",
                CurrentStatus = LeaveRequestStatus.Approved,
                DelegateEmployeeId = 3
            });
        await db.SaveChangesAsync();

        var service = CreateManagerDelegationService(db);
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
            ApplicationRoleId = ApplicationRoleDefaults.EmployeeRoleId,
            DepartmentId = departmentId,
            ManagerId = managerId,
            FirstName = parts[0],
            LastName = parts.Length == 2 ? parts[1] : "Çalışan",
            SicilNo = $"S{id}",
            Email = $"employee{id}@example.com",
            KktcKimlikNo = id.ToString("D10"),
            Status = EmploymentStatus.Active
        };
    }

    private static LeaveRequest PendingLeave(
        int requestId,
        int employeeId,
        int managerEmployeeId,
        DateTime date) => new()
        {
            RequestId = requestId,
            EmployeeId = employeeId,
            Category = LeaveRequestCategory.AnnualLeave,
            StartDate = date,
            EndDate = date,
            RequestedDays = 1,
            Reason = "Bekleyen izin",
            CurrentStatus = LeaveRequestStatus.ManagerReview,
            ManagerApproverEmployeeId = managerEmployeeId
        };

    private static LeaveRequest ApprovedLeave(
        int requestId,
        int employeeId,
        int managerEmployeeId,
        DateTime date) => new()
        {
            RequestId = requestId,
            EmployeeId = employeeId,
            Category = LeaveRequestCategory.AnnualLeave,
            StartDate = date,
            EndDate = date,
            RequestedDays = 1,
            Reason = "Onaylı izin",
            CurrentStatus = LeaveRequestStatus.Approved,
            ManagerApproverEmployeeId = managerEmployeeId
        };

    private static LeaveApproval PendingManagerApproval(
        int requestId,
        int managerEmployeeId) => new()
        {
            RequestId = requestId,
            ApproverRole = LeaveApproverRole.Manager,
            ApproverEmployeeId = managerEmployeeId,
            Decision = LeaveApprovalDecision.Pending
        };

    private static LeaveCancellationRequest PendingCancellation(
        int cancellationRequestId,
        int leaveRequestId,
        int managerEmployeeId,
        DateTime date) => new()
        {
            CancellationRequestId = cancellationRequestId,
            LeaveRequestId = leaveRequestId,
            CancellationStartDate = date,
            CancellationEndDate = date,
            RequestedRefundDays = 1,
            Reason = "Bekleyen iptal",
            CurrentStatus = LeaveRequestStatus.ManagerReview,
            ManagerApproverEmployeeId = managerEmployeeId
        };

    private static LeaveCancellationApproval PendingCancellationApproval(
        int cancellationRequestId,
        int managerEmployeeId) => new()
        {
            CancellationRequestId = cancellationRequestId,
            ApproverRole = LeaveApproverRole.Manager,
            ApproverEmployeeId = managerEmployeeId,
            Decision = LeaveApprovalDecision.Pending
        };

    private static ClaimsPrincipal Principal(int employeeId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, $"employee-{employeeId}"),
                new Claim(UserClaimTypes.EmployeeId, employeeId.ToString()),
                new Claim(UserClaimTypes.MustChangePassword, bool.FalseString)
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

    private static DepartmentManagerService CreateDepartmentManagerService(
        HumanResourcesDbContext dbContext)
    {
        var auditLogService = new AuditLogService(dbContext);
        return new DepartmentManagerService(
            dbContext,
            auditLogService,
            new EmployeeResponsibilityRoleService(dbContext, auditLogService));
    }

    private static ManagerDelegationService CreateManagerDelegationService(
        HumanResourcesDbContext dbContext)
    {
        var auditLogService = new AuditLogService(dbContext);
        return new ManagerDelegationService(
            dbContext,
            auditLogService,
            new EmployeeResponsibilityRoleService(dbContext, auditLogService),
            TimeProvider.System);
    }
}
