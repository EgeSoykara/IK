using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class PublicHolidayAuthorizationTests
{
    [Theory]
    [InlineData(ApplicationRoleDefaults.SystemAdministratorRoleId, true)]
    [InlineData(ApplicationRoleDefaults.ManagerRoleId, false)]
    [InlineData(ApplicationRoleDefaults.HumanResourcesRoleId, true)]
    [InlineData(ApplicationRoleDefaults.EmployeeRoleId, false)]
    public async Task RolePermission_RestrictsPublicHolidayManagement(
        int applicationRoleId,
        bool expected)
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        database.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Test"
        });
        database.Employees.Add(new Employee
        {
            EmployeeId = 1,
            ApplicationRoleId = applicationRoleId,
            DepartmentId = 1,
            SicilNo = "1",
            FirstName = "Test",
            LastName = "Çalışan",
            Email = "test.employee@example.com",
            KktcKimlikNo = "0000000001"
        });
        await database.SaveChangesAsync();
        var authorization = await new ApplicationAuthorizationService(database)
            .FindForEmployeeAsync(1);

        Assert.Equal(
            expected,
            authorization!.Permissions.Contains(PermissionNames.CanManagePublicHolidays));
    }

    [Fact]
    public async Task PageAccess_RequiresAuthenticatedPublicHolidayPermission()
    {
        await using var database = CreateDatabase();
        var service = new PageAccessService(
            TestHumanResourcesDbContextFactory.From(database));

        Assert.True(await service.CanAccessPublicHolidaysAsync(
            PrincipalWithPermission(PermissionNames.CanManagePublicHolidays)));
        Assert.False(await service.CanAccessPublicHolidaysAsync(
            PrincipalWithPermission(PermissionNames.CanManageLeaveRequests)));
        Assert.False(await service.CanAccessPublicHolidaysAsync(new ClaimsPrincipal()));
    }

    [Fact]
    public void PageAndNavigation_UseBackendPermissionAuthority()
    {
        var page = ReadRepoFile("Components", "Pages", "PublicHolidays.razor");
        var nav = ReadRepoFile("Components", "Layout", "NavMenu.razor");

        Assert.Contains("PageAccessService.CanAccessPublicHolidaysAsync(CurrentUser)", page);
        Assert.Equal(2, CountOccurrences(page, "PageAccessService.CanManagePublicHolidays(CurrentUser)"));
        Assert.Contains("@if (_canAccessPublicHolidays)", nav);
        Assert.Contains("Href=\"/PublicHolidays\"", nav);
    }

    [Fact]
    public void LeaveRequestMutations_UseSerializableHolidayCalendarSnapshot()
    {
        var service = ReadRepoFile("Services", "LeaveRequestService.cs");

        Assert.Equal(
            4,
            CountOccurrences(service, "System.Data.IsolationLevel.Serializable"));
    }

    private static HumanResourcesDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HumanResourcesDbContext(options);
    }

    private static ClaimsPrincipal PrincipalWithPermission(string permission) =>
        new(new ClaimsIdentity(
            [
                new Claim(PermissionClaimTypes.Permission, permission),
                new Claim(UserClaimTypes.MustChangePassword, bool.FalseString)
            ],
            authenticationType: "Test"));

    private static string ReadRepoFile(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
