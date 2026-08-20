using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class AuthenticationArchitectureTests
{
    [Fact]
    public async Task BundledRoles_SeedLeastPrivilegePermissionSets()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();

        var persistedPermissions = await dbContext.ApplicationRolePermissions
            .ToListAsync();
        var counts = persistedPermissions
            .GroupBy(permission => permission.ApplicationRoleId)
            .ToDictionary(group => group.Key, group => group.Count());
        var managerPermissions = await dbContext.ApplicationRolePermissions
            .Where(permission => permission.ApplicationRoleId == ApplicationRoleDefaults.ManagerRoleId)
            .Select(permission => permission.PermissionName)
            .ToListAsync();

        Assert.Equal(1, counts[ApplicationRoleDefaults.EmployeeRoleId]);
        Assert.Equal(3, counts[ApplicationRoleDefaults.ManagerRoleId]);
        Assert.Equal(16, counts[ApplicationRoleDefaults.HumanResourcesRoleId]);
        Assert.Equal(15, counts[ApplicationRoleDefaults.SystemAdministratorRoleId]);
        Assert.DoesNotContain(PermissionNames.CanCreateNewEmployee, managerPermissions);
        Assert.DoesNotContain(PermissionNames.CanViewAllPersonnelInformation, managerPermissions);
        Assert.Contains(PermissionNames.CanExectuteApproveLeave, managerPermissions);
    }

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
        await SeedEmployeeAsync(dbContext, ApplicationRoleDefaults.SystemAdministratorRoleId);
        var service = new ApplicationAuthorizationService(dbContext);

        var initial = await service.FindForEmployeeAsync(1);
        Assert.Contains(PermissionNames.CanManagePublicHolidays, initial!.Permissions);

        var persistedPermission = await dbContext.ApplicationRolePermissions.SingleAsync(
            permission =>
                permission.ApplicationRoleId == ApplicationRoleDefaults.SystemAdministratorRoleId
                && permission.PermissionName == PermissionNames.CanManagePublicHolidays);
        dbContext.ApplicationRolePermissions.Remove(persistedPermission);
        await dbContext.SaveChangesAsync();

        var updated = await service.FindForEmployeeAsync(1);
        Assert.DoesNotContain(PermissionNames.CanManagePublicHolidays, updated!.Permissions);
    }

    [Fact]
    public async Task EmployeeAuthenticator_UsesEmailAndHashedTemporaryPassword()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedEmployeeAsync(dbContext, ApplicationRoleDefaults.EmployeeRoleId);
        var passwordHasher = new PasswordHasher<EmployeeCredential>();
        var credentialService = new EmployeeCredentialService(passwordHasher);
        IUserAuthenticator authenticator = new EmployeeUserAuthenticator(
            dbContext,
            credentialService,
            passwordHasher);

        var result = await authenticator.AuthenticateAsync(
            "TEST.USER@EXAMPLE.COM",
            "0000000001");

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Employee!.EmployeeId);
        Assert.Equal("test.user@example.com", result.User!.Email);
        Assert.True(result.User.RequiresPasswordChange);
        Assert.DoesNotContain("0000000001", result.Employee.Credential!.PasswordHash);
    }

    [Fact]
    public async Task ChangedPassword_DisablesIdentityNumberAndSurvivesNewAuthenticator()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedEmployeeAsync(dbContext, ApplicationRoleDefaults.EmployeeRoleId);
        var passwordHasher = new PasswordHasher<EmployeeCredential>();
        var credentialService = new EmployeeCredentialService(passwordHasher);
        var employee = await dbContext.Employees.Include(item => item.Credential).SingleAsync();
        credentialService.ChangePassword(
            employee,
            employee.Credential!,
            "YeniGuvenliSifre-2026");
        await dbContext.SaveChangesAsync();

        IUserAuthenticator authenticator = new EmployeeUserAuthenticator(
            dbContext,
            credentialService,
            passwordHasher);
        var temporaryPasswordResult = await authenticator.AuthenticateAsync(
            employee.Email,
            employee.KktcKimlikNo);
        var permanentPasswordResult = await authenticator.AuthenticateAsync(
            employee.Email,
            "YeniGuvenliSifre-2026");

        Assert.False(temporaryPasswordResult.Succeeded);
        Assert.True(permanentPasswordResult.Succeeded);
        Assert.False(permanentPasswordResult.User!.RequiresPasswordChange);
    }

    [Fact]
    public async Task PassiveEmployee_CannotAuthenticateWithValidPassword()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedEmployeeAsync(dbContext, ApplicationRoleDefaults.EmployeeRoleId);
        var employee = await dbContext.Employees.SingleAsync();
        employee.Status = EmploymentStatus.Passive;
        await dbContext.SaveChangesAsync();
        var passwordHasher = new PasswordHasher<EmployeeCredential>();

        IUserAuthenticator authenticator = new EmployeeUserAuthenticator(
            dbContext,
            new EmployeeCredentialService(passwordHasher),
            passwordHasher);
        var result = await authenticator.AuthenticateAsync(
            employee.Email,
            employee.KktcKimlikNo);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task EmployeeAuthenticator_RejectsOversizedPassword()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedEmployeeAsync(dbContext, ApplicationRoleDefaults.EmployeeRoleId);
        var passwordHasher = new PasswordHasher<EmployeeCredential>();
        IUserAuthenticator authenticator = new EmployeeUserAuthenticator(
            dbContext,
            new EmployeeCredentialService(passwordHasher),
            passwordHasher);

        var result = await authenticator.AuthenticateAsync(
            "test.user@example.com",
            new string('x', EmployeeCredentialService.MaximumPasswordLength + 1));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ClaimsFactory_UsesDatabaseAuthorizationSnapshot()
    {
        var principal = new PermissionClaimsPrincipalFactory().Create(
            new AuthenticatedUser("employee@example.com", "Directory User", false),
            new Employee { EmployeeId = 42 },
            new EmployeeAuthorization(
                ApplicationRoleDefaults.HumanResourcesRoleId,
                ApplicationRoleDefaults.HumanResourcesName,
                [PermissionNames.CanViewAuditLogs]));

        Assert.Equal(
            ApplicationRoleDefaults.HumanResourcesName,
            principal.FindFirstValue(ClaimTypes.Role));
        Assert.Equal("42", principal.FindFirstValue(UserClaimTypes.EmployeeId));
        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(bool.FalseString, principal.FindFirstValue(UserClaimTypes.MustChangePassword));
        Assert.True(principal.HasClaim(
            PermissionClaimTypes.Permission,
            PermissionNames.CanViewAuditLogs));
    }

    [Fact]
    public void PageAccess_RejectsTemporaryPasswordSessionUntilChangeCompletes()
    {
        var access = new PageAccessService(new TestDbContextFactory());
        var temporaryPrincipal = CreateAuthenticatedPrincipal(requiresPasswordChange: true);
        var changedPrincipal = CreateAuthenticatedPrincipal(requiresPasswordChange: false);

        Assert.False(access.CanAccessAuthenticatedPages(temporaryPrincipal));
        Assert.False(access.CanManageEmployees(temporaryPrincipal));
        Assert.True(access.CanAccessAuthenticatedPages(changedPrincipal));
    }

    [Fact]
    public void LoginEndpoint_DependsOnAuthenticatorInterfaceAndDatabaseAuthorization()
    {
        var program = ReadRepoFile("Program.cs");

        Assert.Contains("IUserAuthenticator userAuthenticator", program);
        Assert.Contains("ApplicationAuthorizationService applicationAuthorizationService", program);
        Assert.Contains("IUserAuthenticator, EmployeeUserAuthenticator", program);
    }

    [Fact]
    public void AuthenticationSurfaces_RequireEmailAndPersistedPasswordChangePolicy()
    {
        var login = ReadRepoFile("Components", "Pages", "Login.razor");
        var changePassword = ReadRepoFile("Components", "Pages", "ChangePassword.razor");
        var program = ReadRepoFile("Program.cs");

        Assert.Contains("Label=\"E-posta\"", login);
        Assert.Contains("name=\"Email\"", login);
        Assert.DoesNotContain("Kullanıcı Adı", login);
        Assert.Contains("AuthenticationPolicyNames.PasswordChangeRequired", changePassword);
        Assert.Contains("name=\"NewPassword\"", changePassword);
        Assert.Contains("name=\"ConfirmPassword\"", changePassword);
        Assert.Contains("RequireClaim(UserClaimTypes.MustChangePassword, bool.FalseString)", program);
        Assert.Contains("RequireAuthorization(AuthenticationPolicyNames.PasswordChangeRequired)", program);
        Assert.Contains("AuthenticationPolicyNames.AuthenticatedSession", program);
        Assert.Contains("RequireRateLimiting(\"login\")", program);
        Assert.Contains("Oturumu kapat", changePassword);
    }

    [Fact]
    public void EmployeeCredentialModel_RequiresEmailAndCascadesWithEmployee()
    {
        using var dbContext = CreateDbContext();
        var employee = dbContext.Model.FindEntityType(typeof(Employee))!;
        var credential = dbContext.Model.FindEntityType(typeof(EmployeeCredential))!;
        var email = employee.FindProperty(nameof(Employee.Email))!;
        var foreignKey = credential.GetForeignKeys().Single();

        Assert.False(email.IsNullable);
        Assert.True(foreignKey.IsUnique);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HumanResourcesDbContext(options);
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(bool requiresPasswordChange)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(
                    UserClaimTypes.MustChangePassword,
                    requiresPasswordChange ? bool.TrueString : bool.FalseString),
                new Claim(
                    PermissionClaimTypes.Permission,
                    PermissionNames.CanCreateNewEmployee)
            ],
            "Test");
        return new ClaimsPrincipal(identity);
    }

    private sealed class TestDbContextFactory
        : IDbContextFactory<HumanResourcesDbContext>
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

        public HumanResourcesDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;
            return new HumanResourcesDbContext(options);
        }
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
        var employee = new Employee
        {
            EmployeeId = 1,
            ApplicationRoleId = applicationRoleId,
            DepartmentId = 1,
            SicilNo = "1",
            FirstName = "Test",
            LastName = "Kullanıcı",
            Email = "test.user@example.com",
            KktcKimlikNo = "0000000001"
        };
        employee.Credential = new EmployeeCredentialService(
                new PasswordHasher<EmployeeCredential>())
            .CreateInitial(employee);
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var root = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }
}
