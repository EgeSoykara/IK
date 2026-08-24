using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace IK.Web.Tests;

public sealed class AuthenticationArchitectureTests
{
    [Fact]
    public async Task BundledRoles_SeedLeastPrivilegePermissionSets()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();

        var persistedPermissions = await dbContext.ApplicationRolePermissions.ToListAsync();
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
            .Where(permission => permission.PermissionName == PermissionNames.CanActAsHumanResources)
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
    public async Task ActiveDirectoryAuthenticator_AuthenticatesExistingActiveEmployeeBySamAccountName()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedEmployeeAsync(dbContext, ApplicationRoleDefaults.EmployeeRoleId);
        var authenticator = CreateAuthenticator(
            dbContext,
            new ActiveDirectoryUser("test.user", "Directory", "User", "directory@example.com", null));

        var result = await authenticator.AuthenticateAsync(" TEST.USER ", "domain-password");

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Employee!.EmployeeId);
        Assert.Equal("test.user@example.com", result.User!.Email);
        Assert.Single(await dbContext.Employees.ToListAsync());
    }

    [Fact]
    public async Task ActiveDirectoryAuthenticator_ProvisionsMissingEmployeeWithEmployeeRoleAndDeferredPersonnelFields()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        var authenticator = CreateAuthenticator(
            dbContext,
            new ActiveDirectoryUser(
                "new.user",
                "New",
                "User",
                "NEW.USER@EXAMPLE.COM",
                "1234"));

        var result = await authenticator.AuthenticateAsync("new.user", "domain-password");

        Assert.True(result.Succeeded);
        var employee = await dbContext.Employees.Include(item => item.Phones).SingleAsync();
        Assert.Equal("new.user", employee.SamAccountName);
        Assert.Equal("new.user@example.com", employee.Email);
        Assert.Equal(ApplicationRoleDefaults.EmployeeRoleId, employee.ApplicationRoleId);
        Assert.Equal(EmploymentStatus.Active, employee.Status);
        Assert.Null(employee.DepartmentId);
        Assert.Null(employee.ManagerId);
        Assert.Null(employee.SicilNo);
        Assert.Null(employee.KktcKimlikNo);
        Assert.Equal("1234", Assert.Single(employee.Phones).PhoneNumber);
        var audit = await dbContext.AuditLogs.SingleAsync();
        Assert.Equal(AuditActionType.EmployeeCreated, audit.ActionType);
        Assert.Equal(SystemActorKeys.ActiveDirectoryProvisioning, audit.SystemActorKey);
    }

    [Fact]
    public async Task ActiveDirectoryAuthenticator_DoesNotProvisionInvalidOrIncompleteDirectoryIdentity()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        var invalidCredentials = CreateAuthenticator(dbContext, null);
        var incompleteIdentity = CreateAuthenticator(
            dbContext,
            new ActiveDirectoryUser("incomplete", null, "User", "user@example.com", null));

        var invalidResult = await invalidCredentials.AuthenticateAsync("missing", "wrong");
        var incompleteResult = await incompleteIdentity.AuthenticateAsync("incomplete", "valid");

        Assert.False(invalidResult.Succeeded);
        Assert.False(incompleteResult.Succeeded);
        Assert.Empty(await dbContext.Employees.ToListAsync());
        Assert.Empty(await dbContext.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ActiveDirectoryAuthenticator_RejectsOversizedCredentialsBeforeDirectoryBind()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        var directoryClient = new StubActiveDirectoryClient(
            new ActiveDirectoryUser("test.user", "Test", "User", "test.user@example.com", null));
        var authenticator = new ActiveDirectoryUserAuthenticator(
            dbContext,
            directoryClient,
            new AuditLogService(dbContext),
            NullLogger<ActiveDirectoryUserAuthenticator>.Instance);

        var oversizedIdentifier = await authenticator.AuthenticateAsync(new string('a', 257), "valid");
        var oversizedPassword = await authenticator.AuthenticateAsync("test.user", new string('p', 129));

        Assert.False(oversizedIdentifier.Succeeded);
        Assert.False(oversizedPassword.Succeeded);
        Assert.Equal(0, directoryClient.AuthenticationAttempts);
        Assert.Empty(await dbContext.Employees.ToListAsync());
    }

    [Fact]
    public async Task PassiveEmployee_CannotAuthenticateWithValidActiveDirectoryPassword()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedEmployeeAsync(dbContext, ApplicationRoleDefaults.EmployeeRoleId);
        var employee = await dbContext.Employees.SingleAsync();
        employee.Status = EmploymentStatus.Passive;
        await dbContext.SaveChangesAsync();
        var authenticator = CreateAuthenticator(
            dbContext,
            new ActiveDirectoryUser("test.user", "Test", "User", employee.Email, null));

        var result = await authenticator.AuthenticateAsync("test.user", "valid");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ClaimsFactory_UsesDatabaseAuthorizationSnapshot()
    {
        var principal = new PermissionClaimsPrincipalFactory().Create(
            new AuthenticatedUser("employee@example.com", "Directory User"),
            new Employee { EmployeeId = 42 },
            new EmployeeAuthorization(
                ApplicationRoleDefaults.HumanResourcesRoleId,
                ApplicationRoleDefaults.HumanResourcesName,
                [PermissionNames.CanViewAuditLogs]));

        Assert.Equal(ApplicationRoleDefaults.HumanResourcesName, principal.FindFirstValue(ClaimTypes.Role));
        Assert.Equal("42", principal.FindFirstValue(UserClaimTypes.EmployeeId));
        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(principal.HasClaim(
            PermissionClaimTypes.Permission,
            PermissionNames.CanViewAuditLogs));
    }

    [Fact]
    public void PageAccess_AcceptsAuthenticatedSessionWithoutLocalPasswordClaim()
    {
        var access = new PageAccessService(new TestDbContextFactory());
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));

        Assert.True(access.CanAccessAuthenticatedPages(principal));
    }

    [Fact]
    public void LoginEndpoint_BindsActiveDirectoryAuthenticatorAndDatabaseAuthorization()
    {
        var program = ReadRepoFile("Program.cs");

        Assert.Contains("IUserAuthenticator userAuthenticator", program);
        Assert.Contains("ApplicationAuthorizationService applicationAuthorizationService", program);
        Assert.Contains("IUserAuthenticator, ActiveDirectoryUserAuthenticator", program);
        Assert.Contains("IActiveDirectoryClient, PrincipalContextActiveDirectoryClient", program);
        Assert.DoesNotContain("EmployeeCredential", program);
        Assert.DoesNotContain("change-password", program);
    }

    [Fact]
    public void ActiveDirectoryClient_UsesSignedNegotiateBindingForKoopSamCredentials()
    {
        var client = ReadRepoFile("Services/PrincipalContextActiveDirectoryClient.cs");
        var settings = ReadRepoFile("appsettings.json");

        Assert.Contains("ContextType.Domain", client);
        Assert.Contains(
            "ContextOptions.Negotiate | ContextOptions.Signing",
            client);
        Assert.Contains("IdentityType.SamAccountName", client);
        Assert.Contains("\"Domain\": \"koop\"", settings);
    }

    [Fact]
    public void AuthenticationSurface_RequiresSamAccountNameAndHasNoLocalPasswordChangeRoute()
    {
        var login = ReadRepoFile("Components", "Pages", "Login.razor");
        var pagesDirectory = Path.GetDirectoryName(
            Path.Combine(RepoRoot(), "Components", "Pages", "Login.razor"))!;

        Assert.Contains("Label=\"Kullanıcı adı\"", login);
        Assert.Contains("name=\"Identifier\"", login);
        Assert.DoesNotContain("name=\"Email\"", login);
        Assert.False(File.Exists(Path.Combine(pagesDirectory, "ChangePassword.razor")));
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/LeaveRequests?year=2026", "/LeaveRequests?year=2026")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("https://example.com", null)]
    [InlineData("//example.com", null)]
    [InlineData("/\\example.com", null)]
    [InlineData("/login", null)]
    [InlineData("/auth/login", null)]
    public void LoginReturnUrl_AllowsOnlySafeApplicationDestinations(
        string? returnUrl,
        string? expected)
    {
        Assert.Equal(expected, LoginReturnUrl.Normalize(returnUrl));
    }

    [Fact]
    public void EmployeeModel_AllowsDeferredAdProvisioningFieldsAndKeepsUniqueSamAccountName()
    {
        using var dbContext = CreateDbContext();
        var employee = dbContext.Model.FindEntityType(typeof(Employee))!;
        var samAccountName = employee.FindProperty(nameof(Employee.SamAccountName))!;
        var departmentId = employee.FindProperty(nameof(Employee.DepartmentId))!;
        var sicilNo = employee.FindProperty(nameof(Employee.SicilNo))!;
        var identityNumber = employee.FindProperty(nameof(Employee.KktcKimlikNo))!;
        var samIndex = employee.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Employee.SamAccountName)]));

        Assert.True(samAccountName.IsNullable);
        Assert.True(departmentId.IsNullable);
        Assert.True(sicilNo.IsNullable);
        Assert.True(identityNumber.IsNullable);
        Assert.True(samIndex.IsUnique);
        Assert.Null(dbContext.Model.FindEntityType("IK.Web.Models.EmployeeCredential"));
    }

    private static IUserAuthenticator CreateAuthenticator(
        HumanResourcesDbContext dbContext,
        ActiveDirectoryUser? directoryUser) =>
        new ActiveDirectoryUserAuthenticator(
            dbContext,
            new StubActiveDirectoryClient(directoryUser),
            new AuditLogService(dbContext),
            NullLogger<ActiveDirectoryUserAuthenticator>.Instance);

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new HumanResourcesDbContext(options);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<HumanResourcesDbContext>
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

    private sealed class StubActiveDirectoryClient(ActiveDirectoryUser? user) : IActiveDirectoryClient
    {
        public int AuthenticationAttempts { get; private set; }

        public ActiveDirectoryUser? Authenticate(string identifier, string password)
        {
            AuthenticationAttempts++;
            return user;
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
        dbContext.Employees.Add(new Employee
        {
            EmployeeId = 1,
            ApplicationRoleId = applicationRoleId,
            DepartmentId = 1,
            SicilNo = "1",
            SamAccountName = "test.user",
            FirstName = "Test",
            LastName = "Kullanıcı",
            Email = "test.user@example.com",
            KktcKimlikNo = "0000000001"
        });
        await dbContext.SaveChangesAsync();
    }

    private static string RepoRoot() => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string ReadRepoFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([RepoRoot(), .. segments]));
}
