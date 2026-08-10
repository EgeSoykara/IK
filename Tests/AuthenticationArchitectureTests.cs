using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class AuthenticationArchitectureTests
{
    [Fact]
    public async Task HumanResourcesCapability_IsSeededOnlyForHumanResourcesRole()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();

        var assignedRoleIds = await dbContext.ApplicationRolePermissions
            .Where(permission =>
                permission.PermissionName == PermissionNames.CanActAsHumanResources)
            .Select(permission => permission.ApplicationRoleId)
            .ToListAsync();

        Assert.Equal([ApplicationRoleDefaults.HumanResourcesRoleId], assignedRoleIds);
    }

    [Fact]
    public async Task ApplicationAuthorization_ReflectsPersistedPermissionChanges()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedEmployeeAsync(dbContext, ApplicationRoleDefaults.AdministratorRoleId);
        var service = new ApplicationAuthorizationService(dbContext);

        var initial = await service.FindForEmployeeAsync(1);
        Assert.Contains(PermissionNames.CanManagePublicHolidays, initial!.Permissions);

        var persistedPermission = await dbContext.ApplicationRolePermissions.SingleAsync(
            permission =>
                permission.ApplicationRoleId == ApplicationRoleDefaults.AdministratorRoleId
                && permission.PermissionName == PermissionNames.CanManagePublicHolidays);
        dbContext.ApplicationRolePermissions.Remove(persistedPermission);
        await dbContext.SaveChangesAsync();

        var updated = await service.FindForEmployeeAsync(1);
        Assert.DoesNotContain(PermissionNames.CanManagePublicHolidays, updated!.Permissions);
    }

    [Fact]
    public async Task StaticAuthenticator_OwnsOnlyCredentialsAndEmployeeResolution()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedEmployeeAsync(dbContext, ApplicationRoleDefaults.EmployeeRoleId);
        IUserAuthenticator authenticator = new StaticUserAuthenticator(dbContext);

        var result = await authenticator.AuthenticateAsync("admin", "admin123");

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Employee!.EmployeeId);
        Assert.Equal("admin", result.User!.UserName);
    }

    [Fact]
    public void ClaimsFactory_UsesDatabaseAuthorizationSnapshot()
    {
        var principal = new PermissionClaimsPrincipalFactory().Create(
            new AuthenticatedUser("directory-user", "Directory User"),
            new Employee { EmployeeId = 42 },
            new EmployeeAuthorization(
                ApplicationRoleDefaults.HumanResourcesRoleId,
                ApplicationRoleDefaults.HumanResourcesName,
                [PermissionNames.CanViewAuditLogs]));

        Assert.Equal(
            ApplicationRoleDefaults.HumanResourcesName,
            principal.FindFirstValue(ClaimTypes.Role));
        Assert.Equal("42", principal.FindFirstValue(UserClaimTypes.EmployeeId));
        Assert.True(principal.HasClaim(
            PermissionClaimTypes.Permission,
            PermissionNames.CanViewAuditLogs));
    }

    [Fact]
    public void LoginEndpoint_DependsOnAuthenticatorInterfaceAndDatabaseAuthorization()
    {
        var program = ReadRepoFile("Program.cs");

        Assert.Contains("IUserAuthenticator userAuthenticator", program);
        Assert.Contains("ApplicationAuthorizationService applicationAuthorizationService", program);
        Assert.DoesNotContain("StaticUserAuthenticator static", program);
    }

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HumanResourcesDbContext(options);
    }

    private static async Task SeedEmployeeAsync(
        HumanResourcesDbContext dbContext,
        int applicationRoleId)
    {
        dbContext.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Test"
        });
        dbContext.Employees.Add(new Employee
        {
            EmployeeId = 1,
            ApplicationRoleId = applicationRoleId,
            DepartmentId = 1,
            SicilNo = "1",
            FirstName = "Test",
            LastName = "Kullanıcı",
            KktcKimlikNo = "0000000001"
        });
        await dbContext.SaveChangesAsync();
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var root = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }
}
