using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class LeaveTrackingTests
{
    [Fact]
    public async Task OrdinaryEmployee_IsScopedToOwnDepartment()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);
        var service = CreateService(db);
        var principal = Principal(employeeId: 1);

        var snapshot = await service.GetSnapshotAsync(
            principal,
            new DateOnly(2026, 7, 1),
            requestedDepartmentId: null,
            new DateOnly(2026, 7, 15));

        Assert.False(snapshot.CanViewAllDepartments);
        Assert.Equal(1, snapshot.DepartmentId);
        Assert.All(snapshot.Events, item => Assert.Equal("Operasyon", item.DepartmentName));
        Assert.All(snapshot.Roster, item => Assert.Equal("Operasyon", item.DepartmentName));
        Assert.Contains(snapshot.Events, item =>
            item.RequestId == 10
            && item.RequestedBy == "employee-one"
            && item.ApprovedBy == "İK Yetkilisi");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetSnapshotAsync(
                principal,
                new DateOnly(2026, 7, 1),
                requestedDepartmentId: 2,
                new DateOnly(2026, 7, 15)));
    }

    [Fact]
    public async Task ElevatedEmployee_CanSeeAllDepartmentsAndRosterStatuses()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);
        var service = CreateService(db);
        var principal = Principal(1, PermissionNames.CanViewLeaveRequests);

        var snapshot = await service.GetSnapshotAsync(
            principal,
            new DateOnly(2026, 7, 1),
            requestedDepartmentId: null,
            new DateOnly(2026, 7, 15));

        Assert.True(snapshot.CanViewAllDepartments);
        Assert.Equal(2, snapshot.Departments.Count);
        Assert.Contains(snapshot.Events, item => item.DepartmentName == "Operasyon");
        Assert.Contains(snapshot.Events, item => item.DepartmentName == "Finans");
        Assert.Contains(snapshot.Roster, item =>
            item.EmployeeId == 1 && item.Status == WorkforceLeaveStatus.OnLeave);
        Assert.Contains(snapshot.Roster, item =>
            item.EmployeeId == 2 && item.Status == WorkforceLeaveStatus.Pending);
        Assert.All(snapshot.Events, item => Assert.False(string.IsNullOrWhiteSpace(item.RequestedBy)));
    }

    [Fact]
    public async Task CalendarAndRoster_ExcludeWeekendsAndConfiguredPublicHolidays()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);
        var service = CreateService(db);

        var snapshot = await service.GetSnapshotAsync(
            Principal(1, PermissionNames.CanViewLeaveRequests),
            new DateOnly(2026, 7, 1),
            requestedDepartmentId: null,
            new DateOnly(2026, 7, 20));

        var weekendSpanningEvent = Assert.Single(
            snapshot.Events.Where(item => item.RequestId == 12));
        Assert.Equal([new DateOnly(2026, 7, 17)], weekendSpanningEvent.WorkingDates);
        Assert.All(
            snapshot.Roster,
            item => Assert.Equal(WorkforceLeaveStatus.NonWorkingDay, item.Status));
    }

    [Fact]
    public void LeaveTrackingUi_DeclaresStatusColorsActorsAndManualTransfer()
    {
        var repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var page = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Components",
            "Pages",
            "LeaveTracking.razor"));
        var css = File.ReadAllText(Path.Combine(repositoryRoot, "wwwroot", "app.css"));

        Assert.Contains("@page \"/LeaveTracking\"", page);
        Assert.Contains("Talep: @item.RequestedBy", page);
        Assert.Contains("Onay: @item.ApprovedBy", page);
        Assert.Contains("Vekâleti Devret", page);
        Assert.DoesNotContain("@item.Reason", page);
        Assert.Contains(".leave-event-pending", css);
        Assert.Contains(".leave-event-approved", css);
        Assert.Contains("@media (max-width: 900px)", css);
    }

    private static async Task SeedAsync(HumanResourcesDbContext db)
    {
        db.Departments.AddRange(
            new Department { DepartmentId = 1, DepartmentName = "Operasyon" },
            new Department { DepartmentId = 2, DepartmentName = "Finans" });
        db.Employees.AddRange(
            Employee(1, 1, "Ali", "Çalışan"),
            Employee(2, 2, "Ayşe", "Çalışan"),
            Employee(3, 1, "İK", "Yetkilisi"));
        db.LeaveTypes.Add(new LeaveType
        {
            LeaveTypeId = 1,
            Name = "Yıllık",
            AnnualQuota = 20
        });
        db.LeaveRequests.AddRange(
            new LeaveRequest
            {
                RequestId = 10,
                EmployeeId = 1,
                LeaveTypeId = 1,
                StartDate = new DateTime(2026, 7, 14),
                EndDate = new DateTime(2026, 7, 16),
                RequestedDays = 3,
                Reason = "Gizli neden",
                CurrentStatus = LeaveRequestStatus.Approved
            },
            new LeaveRequest
            {
                RequestId = 11,
                EmployeeId = 2,
                LeaveTypeId = 1,
                StartDate = new DateTime(2026, 7, 15),
                EndDate = new DateTime(2026, 7, 17),
                RequestedDays = 3,
                Reason = "Gizli neden",
                CurrentStatus = LeaveRequestStatus.ManagerReview
            },
            new LeaveRequest
            {
                RequestId = 12,
                EmployeeId = 1,
                LeaveTypeId = 1,
                StartDate = new DateTime(2026, 7, 17),
                EndDate = new DateTime(2026, 7, 20),
                RequestedDays = 1,
                Reason = "Gizli neden",
                CurrentStatus = LeaveRequestStatus.Approved
            });
        db.PublicHolidays.Add(new PublicHoliday
        {
            Date = new DateOnly(2026, 7, 20),
            Name = "Yapılandırılmış Tatil"
        });
        db.LeaveApprovals.Add(new LeaveApproval
        {
            RequestId = 10,
            ApproverRole = LeaveApproverRole.HumanResources,
            ApproverEmployeeId = 3,
            Decision = LeaveApprovalDecision.Approved,
            DecisionDate = DateTimeOffset.UtcNow
        });
        db.AuditLogs.AddRange(
            new AuditLog
            {
                ActionType = AuditActionType.LeaveRequestCreated,
                EntityName = nameof(LeaveRequest),
                EntityId = "10",
                UserId = "employee-one"
            },
            new AuditLog
            {
                ActionType = AuditActionType.LeaveRequestCreated,
                EntityName = nameof(LeaveRequest),
                EntityId = "11",
                UserId = "employee-two"
            },
            new AuditLog
            {
                ActionType = AuditActionType.LeaveRequestCreated,
                EntityName = nameof(LeaveRequest),
                EntityId = "12",
                UserId = "employee-one"
            });
        await db.SaveChangesAsync();
    }

    private static Employee Employee(
        int id,
        int departmentId,
        string firstName,
        string lastName) =>
        new()
        {
            EmployeeId = id,
            DepartmentId = departmentId,
            FirstName = firstName,
            LastName = lastName,
            SicilNo = $"S{id}",
            KktcKimlikNo = id.ToString("D10"),
            Status = EmploymentStatus.Active
        };

    private static ClaimsPrincipal Principal(int employeeId, string? permission = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, $"employee-{employeeId}"),
            new(UserClaimTypes.EmployeeId, employeeId.ToString())
        };
        if (permission is not null)
        {
            claims.Add(new Claim(PermissionClaimTypes.Permission, permission));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HumanResourcesDbContext(options);
    }

    private static LeaveTrackingService CreateService(HumanResourcesDbContext db) =>
        new(
            db,
            new PageAccessService(db),
            new PublicHolidayCalendar(db));
}
