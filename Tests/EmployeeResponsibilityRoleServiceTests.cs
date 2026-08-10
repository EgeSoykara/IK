using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class EmployeeResponsibilityRoleServiceTests
{
    [Fact]
    public async Task Reconcile_PromotesOnlyEmployeeRoleAndPreservesHumanResourcesRole()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon",
            ManagerEmployeeId = 1,
            ActiveDelegateEmployeeId = 2
        });
        dbContext.Departments.Add(new Department
        {
            DepartmentId = 2,
            DepartmentName = "Özel Rol",
            ManagerEmployeeId = 3
        });
        dbContext.Employees.AddRange(
            Employee(1, ApplicationRoleDefaults.EmployeeRoleId),
            Employee(2, ApplicationRoleDefaults.HumanResourcesRoleId),
            Employee(3, 99));
        await dbContext.SaveChangesAsync();
        var auditLogService = new AuditLogService(dbContext);
        var service = new EmployeeResponsibilityRoleService(dbContext, auditLogService);

        await service.ReconcileForSystemActorAsync(
            [1, 2, 3],
            "test-system",
            "Test");

        Assert.Equal(
            ApplicationRoleDefaults.AdministratorRoleId,
            (await dbContext.Employees.FindAsync(1))!.ApplicationRoleId);
        Assert.Equal(
            ApplicationRoleDefaults.HumanResourcesRoleId,
            (await dbContext.Employees.FindAsync(2))!.ApplicationRoleId);
        Assert.Equal(99, (await dbContext.Employees.FindAsync(3))!.ApplicationRoleId);
        var audit = Assert.Single(dbContext.AuditLogs);
        Assert.Equal("test-system", audit.SystemActorKey);
        Assert.Contains("ApplicationRoleId=1->2", audit.Details);
    }

    [Fact]
    public async Task Reconcile_DemotesOnlyUnassignedAdministrator()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon",
            ManagerEmployeeId = 2
        });
        dbContext.Employees.AddRange(
            Employee(1, ApplicationRoleDefaults.AdministratorRoleId),
            Employee(2, ApplicationRoleDefaults.AdministratorRoleId),
            Employee(3, ApplicationRoleDefaults.HumanResourcesRoleId),
            Employee(4, 99));
        await dbContext.SaveChangesAsync();
        var auditLogService = new AuditLogService(dbContext);
        var service = new EmployeeResponsibilityRoleService(dbContext, auditLogService);

        await service.ReconcileForEmployeeActorAsync(
            [1, 2, 3, 4],
            actorEmployeeId: 2,
            source: "Test");

        Assert.Equal(
            ApplicationRoleDefaults.EmployeeRoleId,
            (await dbContext.Employees.FindAsync(1))!.ApplicationRoleId);
        Assert.Equal(
            ApplicationRoleDefaults.AdministratorRoleId,
            (await dbContext.Employees.FindAsync(2))!.ApplicationRoleId);
        Assert.Equal(
            ApplicationRoleDefaults.HumanResourcesRoleId,
            (await dbContext.Employees.FindAsync(3))!.ApplicationRoleId);
        Assert.Equal(99, (await dbContext.Employees.FindAsync(4))!.ApplicationRoleId);
        var audit = Assert.Single(dbContext.AuditLogs);
        Assert.Equal(2, audit.ActorEmployeeId);
        Assert.Contains("ApplicationRoleId=2->1", audit.Details);
    }

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HumanResourcesDbContext(options);
    }

    private static Employee Employee(int employeeId, int applicationRoleId) => new()
    {
        EmployeeId = employeeId,
        ApplicationRoleId = applicationRoleId,
        DepartmentId = 1,
        SicilNo = $"S{employeeId}",
        FirstName = "Test",
        LastName = $"Çalışan {employeeId}",
        KktcKimlikNo = employeeId.ToString("D10"),
        Status = EmploymentStatus.Active
    };
}
