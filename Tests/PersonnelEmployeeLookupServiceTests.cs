using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class PersonnelEmployeeLookupServiceTests
{
    [Fact]
    public async Task SearchAsync_RequiresManagerInputAndReturnsAtMostTwentyProjectedRows()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon",
            ManagerEmployeeId = 1
        });
        dbContext.Employees.AddRange(Enumerable.Range(1, 35).Select(Employee));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        Assert.Empty(await service.SearchAsync(ManagerPrincipal(), " "));
        Assert.Empty(await service.SearchAsync(ManagerPrincipal(), "T"));

        var results = await service.SearchAsync(ManagerPrincipal(), "Test");

        Assert.Equal(PersonnelEmployeeLookupService.MaximumResults, results.Count);
        Assert.Equal(
            results.OrderBy(item => item.FirstName)
                .ThenBy(item => item.LastName)
                .ThenBy(item => item.EmployeeId),
            results);
        Assert.All(results, item => Assert.StartsWith("Test Çalışan", item.DisplayName));
        Assert.Equal(
            2,
            Assert.Single(await service.SearchAsync(EmployeePrincipal(2), "Test")).EmployeeId);
    }

    [Fact]
    public async Task SearchAsync_MatchesSicilAndEmailAndHonorsCancellation()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon",
            ManagerEmployeeId = 1
        });
        var employee = Employee(1);
        employee.SicilNo = "ÖZEL-7788";
        employee.Email = "lookup@example.com";
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        Assert.Equal(1, Assert.Single(await service.SearchAsync(
            ManagerPrincipal(),
            "7788")).EmployeeId);
        Assert.Equal(1, Assert.Single(await service.SearchAsync(
            ManagerPrincipal(),
            "lookup@")).EmployeeId);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SearchAsync(ManagerPrincipal(), "Test", cancellation.Token));
    }

    [Fact]
    public async Task SearchAsync_MatchesDepartmentAndIncludesDepartmentInDisplayName()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon Merkezi",
            ManagerEmployeeId = 1
        });
        dbContext.Employees.AddRange(Employee(1), Employee(2));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var results = await service.SearchAsync(ManagerPrincipal(), "Operasyon");

        Assert.Equal(2, results.Count);
        Assert.All(results, result =>
        {
            Assert.Equal("Operasyon Merkezi", result.DepartmentName);
            Assert.EndsWith("· Operasyon Merkezi", result.DisplayName);
        });
    }

    [Fact]
    public async Task DepartmentSearchAndPaging_StayWithinVisibleDepartments()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Departments.AddRange(
            new Department
            {
                DepartmentId = 1,
                DepartmentName = "Operasyon",
                ManagerEmployeeId = 1
            },
            new Department
            {
                DepartmentId = 2,
                DepartmentName = "Finans"
            });
        dbContext.Employees.AddRange(
            Enumerable.Range(1, 18).Select(Employee));
        var hiddenEmployee = Employee(50);
        hiddenEmployee.DepartmentId = 2;
        dbContext.Employees.Add(hiddenEmployee);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        Assert.Equal(
            1,
            Assert.Single(await service.SearchDepartmentsAsync(
                ManagerPrincipal(),
                "Operasyon")).DepartmentId);
        Assert.Empty(await service.SearchDepartmentsAsync(
            ManagerPrincipal(),
            "Finans"));

        var firstPage = await service.GetDepartmentEmployeesAsync(
            ManagerPrincipal(),
            departmentId: 1,
            pageNumber: 1);
        var lastPage = await service.GetDepartmentEmployeesAsync(
            ManagerPrincipal(),
            departmentId: 1,
            pageNumber: 99);

        Assert.Equal(18, firstPage.TotalCount);
        Assert.Equal(PersonnelEmployeeLookupService.DepartmentPageSize, firstPage.Employees.Count);
        Assert.Equal(3, firstPage.PageCount);
        Assert.Equal(3, lastPage.PageNumber);
        Assert.Equal(2, lastPage.Employees.Count);
        Assert.Empty((await service.GetDepartmentEmployeesAsync(
            ManagerPrincipal(),
            departmentId: 2,
            pageNumber: 1)).Employees);
    }

    [Fact]
    public async Task ResolveInitialAsync_PrefersAuthorizedRequestAndFallsBackToOwnEmployee()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Operasyon",
            ManagerEmployeeId = 1
        });
        dbContext.Employees.AddRange(Employee(1), Employee(2), Employee(3));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        Assert.Equal(
            3,
            (await service.ResolveInitialAsync(ManagerPrincipal(1), 3))!.EmployeeId);
        Assert.Equal(
            1,
            (await service.ResolveInitialAsync(ManagerPrincipal(1), 999))!.EmployeeId);
        Assert.Equal(
            2,
            (await service.ResolveInitialAsync(EmployeePrincipal(2), 3))!.EmployeeId);
        Assert.Null(await service.FindAsync(EmployeePrincipal(2), 3));
    }

    private static PersonnelEmployeeLookupService CreateService(
        HumanResourcesDbContext dbContext)
    {
        var factory = TestHumanResourcesDbContextFactory.From(dbContext);
        var pageAccessService = new PageAccessService(factory);
        return new PersonnelEmployeeLookupService(
            factory,
            pageAccessService,
            new PersonnelAuthorizationService(factory, pageAccessService));
    }

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HumanResourcesDbContext(options);
    }

    private static Employee Employee(int employeeId) => new()
    {
        EmployeeId = employeeId,
        ApplicationRoleId = ApplicationRoleDefaults.EmployeeRoleId,
        DepartmentId = 1,
        SicilNo = $"S{employeeId:D4}",
        FirstName = "Test",
        LastName = $"Çalışan {employeeId:D4}",
        KktcKimlikNo = employeeId.ToString("D10"),
        Status = EmploymentStatus.Active
    };

    private static ClaimsPrincipal ManagerPrincipal(int employeeId = 1) =>
        new(new ClaimsIdentity(
            [
                new Claim(UserClaimTypes.EmployeeId, employeeId.ToString()),
                new Claim(UserClaimTypes.MustChangePassword, bool.FalseString)
            ],
            authenticationType: "test"));

    private static ClaimsPrincipal EmployeePrincipal(int employeeId) =>
        new(new ClaimsIdentity(
            [
                new Claim(UserClaimTypes.EmployeeId, employeeId.ToString()),
                new Claim(UserClaimTypes.MustChangePassword, bool.FalseString)
            ],
            authenticationType: "test"));
}
