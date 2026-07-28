using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class PublicHolidayAuthorizationTests
{
    [Theory]
    [InlineData(StaticApplicationRole.Administrator, true)]
    [InlineData(StaticApplicationRole.HumanResources, true)]
    [InlineData(StaticApplicationRole.Employee, false)]
    public async Task RolePermission_RestrictsPublicHolidayManagement(
        StaticApplicationRole role,
        bool expected)
    {
        var permissions = await new StaticPermissionService()
            .GetPermissionsAsync(role, role.ToString());

        Assert.Equal(
            expected,
            permissions.Contains(PermissionNames.CanManagePublicHolidays));
    }

    [Fact]
    public async Task PageAccess_RequiresAuthenticatedPublicHolidayPermission()
    {
        await using var database = CreateDatabase();
        var service = new PageAccessService(database);

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
            [new Claim(PermissionClaimTypes.Permission, permission)],
            authenticationType: "Test"));

    private static string ReadRepoFile(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
