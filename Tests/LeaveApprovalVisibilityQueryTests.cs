using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class LeaveApprovalVisibilityQueryTests
{
    [Fact]
    public async Task VisibleTo_Manager_ReturnsOnlyAssignedManagerApprovals()
    {
        await using var database = CreateDatabase();
        await SeedApprovalsAsync(database);
        var principal = CreatePrincipal("Manager", employeeId: 10);

        var visibleApprovals = await database.LeaveApprovals
            .VisibleTo(principal)
            .Select(approval => new
            {
                approval.ApprovalId,
                RequesterName = approval.Request.Employee.FirstName + " " + approval.Request.Employee.LastName
            })
            .ToListAsync();

        var visible = Assert.Single(visibleApprovals);
        Assert.Equal(1, visible.ApprovalId);
        Assert.Equal("Ada Lovelace", visible.RequesterName);
    }

    [Fact]
    public async Task VisibleTo_HumanResources_ReturnsOnlyHumanResourcesApprovals()
    {
        await using var database = CreateDatabase();
        await SeedApprovalsAsync(database);
        var principal = CreatePrincipal("HumanResources", employeeId: 30);

        var approvalIds = await database.LeaveApprovals
            .VisibleTo(principal)
            .Select(approval => approval.ApprovalId)
            .ToListAsync();

        Assert.Equal([3], approvalIds);
    }

    [Fact]
    public async Task VisibleTo_PrincipalWithoutEmployeeId_ReturnsNoManagerApprovals()
    {
        await using var database = CreateDatabase();
        await SeedApprovalsAsync(database);
        var principal = CreatePrincipal("Manager", employeeId: null);

        Assert.Empty(await database.LeaveApprovals.VisibleTo(principal).ToListAsync());
    }

    [Fact]
    public async Task FilterOptionQueries_ReturnOnlyDistinctVisibleValues()
    {
        await using var database = CreateDatabase();
        await SeedApprovalsAsync(database);
        var principal = CreatePrincipal("Manager", employeeId: 10);
        var requester = database.Employees.Local.Single(employee => employee.EmployeeId == 1);
        var leaveType = database.LeaveTypes.Local.Single(lt => lt.LeaveTypeId == 1);
        var duplicateNameRequest = new LeaveRequest
        {
            RequestId = 3,
            Employee = requester,
            EmployeeId = requester.EmployeeId,
            LeaveType = leaveType,
            LeaveTypeId = leaveType.LeaveTypeId,
            ManagerApproverEmployeeId = 10,
            Reason = "Test"
        };
        database.LeaveApprovals.Add(new LeaveApproval
        {
            ApprovalId = 4,
            Request = duplicateNameRequest,
            RequestId = duplicateNameRequest.RequestId,
            ApproverRole = LeaveApproverRole.Manager
        });
        await database.SaveChangesAsync();

        var requesterNames = await database.LeaveApprovals
            .VisibleRequesterNames(principal)
            .ToListAsync();
        var leaveTypeNames = await database.LeaveApprovals
            .VisibleLeaveTypeNames(principal)
            .ToListAsync();

        Assert.Equal(["Ada Lovelace"], requesterNames);
        Assert.Equal(["Yıllık İzin"], leaveTypeNames);
    }

    [Fact]
    public void FilterOptionQueries_TranslateDistinctAndVisibilityToSql()
    {
        using var database = new HumanResourcesDbContext(
            new DbContextOptionsBuilder<HumanResourcesDbContext>()
                .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=IKSolutionQueryTranslationTest;Trusted_Connection=True;TrustServerCertificate=True")
                .Options);
        var principal = CreatePrincipal("Manager", employeeId: 10);

        var queries = new[]
        {
            database.LeaveApprovals.VisibleRequesterNames(principal).ToQueryString(),
            database.LeaveApprovals.VisibleLeaveTypeNames(principal).ToQueryString(),
            database.LeaveApprovals.VisibleApproverNames(principal).ToQueryString()
        };

        Assert.All(queries, sql =>
        {
            Assert.Contains("SELECT DISTINCT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ManagerApproverEmployeeId", sql, StringComparison.Ordinal);
        });
    }

    private static ClaimsPrincipal CreatePrincipal(string role, int? employeeId)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (employeeId.HasValue)
        {
            claims.Add(new Claim(UserClaimTypes.EmployeeId, employeeId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static async Task SeedApprovalsAsync(HumanResourcesDbContext database)
    {
        var firstRequester = new Employee { EmployeeId = 1, FirstName = "Ada", LastName = "Lovelace", SicilNo = "1", KktcKimlikNo = "0000000001", DepartmentId = 1 };
        var secondRequester = new Employee { EmployeeId = 2, FirstName = "Grace", LastName = "Hopper", SicilNo = "2", KktcKimlikNo = "0000000002", DepartmentId = 1 };
        var leaveType = new LeaveType { LeaveTypeId = 1, Name = "Yıllık İzin", MaxAccrualDays = 30 };
        var firstRequest = new LeaveRequest { RequestId = 1, Employee = firstRequester, EmployeeId = 1, LeaveType = leaveType, LeaveTypeId = 1, ManagerApproverEmployeeId = 10, Reason = "Test" };
        var secondRequest = new LeaveRequest { RequestId = 2, Employee = secondRequester, EmployeeId = 2, LeaveType = leaveType, LeaveTypeId = 1, ManagerApproverEmployeeId = 20, Reason = "Test" };

        database.LeaveApprovals.AddRange(
            new LeaveApproval { ApprovalId = 1, Request = firstRequest, RequestId = 1, ApproverRole = LeaveApproverRole.Manager },
            new LeaveApproval { ApprovalId = 2, Request = secondRequest, RequestId = 2, ApproverRole = LeaveApproverRole.Manager },
            new LeaveApproval { ApprovalId = 3, Request = firstRequest, RequestId = 1, ApproverRole = LeaveApproverRole.HumanResources });
        await database.SaveChangesAsync();
    }

    private static HumanResourcesDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HumanResourcesDbContext(options);
    }
}
