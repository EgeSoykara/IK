using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

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

        var service = new ManagerDelegationService(db, new AuditLogService(db));
        await service.ReconcileAsync(today);

        Assert.Equal(2, (await db.Employees.FindAsync(3))!.ManagerId);
        Assert.Equal(2, (await db.LeaveRequests.FindAsync(11))!.ManagerApproverEmployeeId);
        Assert.True((await db.ManagerDelegations.SingleAsync()).IsActive);

        await service.ReconcileAsync(today.AddDays(1));

        Assert.Equal(1, (await db.Employees.FindAsync(3))!.ManagerId);
        Assert.Equal(1, (await db.LeaveRequests.FindAsync(11))!.ManagerApproverEmployeeId);
        Assert.False((await db.ManagerDelegations.SingleAsync()).IsActive);
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

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new HumanResourcesDbContext(options);
    }
}
