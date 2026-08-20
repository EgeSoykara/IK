using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class PasswordChangeServiceTests
{
    [Fact]
    public async Task ChangeRequiredPassword_PersistsHashFlagAndAuditAtomically()
    {
        await using var fixture = await CreateFixtureAsync();
        var result = await fixture.Service.ChangeRequiredPasswordAsync(
            Principal(1),
            "YeniGuvenliSifre-2026");

        var credential = await fixture.Database.EmployeeCredentials.SingleAsync();
        var audit = await fixture.Database.AuditLogs.SingleAsync();
        Assert.False(credential.MustChangePassword);
        Assert.NotNull(credential.PasswordChangedAt);
        Assert.Equal(AuditActionType.PasswordChanged, audit.ActionType);
        Assert.Equal(1, audit.ActorEmployeeId);
        Assert.False(result.User.RequiresPasswordChange);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            fixture.CredentialService.Verify(credential, "YeniGuvenliSifre-2026"));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            fixture.CredentialService.Verify(credential, "0000000001"));
    }

    [Fact]
    public async Task ChangeRequiredPassword_RejectsIdentityNumberWithoutMutation()
    {
        await using var fixture = await CreateFixtureAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ChangeRequiredPasswordAsync(Principal(1), "0000000001"));

        Assert.Contains("kimlik numarasıyla aynı olamaz", exception.Message);
        var credential = await fixture.Database.EmployeeCredentials.SingleAsync();
        Assert.True(credential.MustChangePassword);
        Assert.Null(credential.PasswordChangedAt);
        Assert.Empty(await fixture.Database.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ChangeRequiredPassword_AllowsOnlyOnePersistedTransition()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.ChangeRequiredPasswordAsync(
            Principal(1),
            "IlkGuvenliSifre-2026");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ChangeRequiredPasswordAsync(
                Principal(1),
                "IkinciGuvenliSifre-2026"));

        var credential = await fixture.Database.EmployeeCredentials.SingleAsync();
        Assert.Single(await fixture.Database.AuditLogs.ToListAsync());
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            fixture.CredentialService.Verify(credential, "IlkGuvenliSifre-2026"));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            fixture.CredentialService.Verify(credential, "IkinciGuvenliSifre-2026"));
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation(
            "Latin1_General_100_BIN2",
            static (left, right) => string.CompareOrdinal(left, right));
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseSqlite(connection)
            .Options;
        var database = new HumanResourcesDbContext(options);
        await database.Database.EnsureCreatedAsync();
        database.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "Test"
        });
        await database.SaveChangesAsync();
        var employee = new Employee
        {
            EmployeeId = 1,
            ApplicationRoleId = ApplicationRoleDefaults.EmployeeRoleId,
            DepartmentId = 1,
            SicilNo = "S1",
            FirstName = "Test",
            LastName = "Çalışan",
            Email = "test.employee@example.com",
            KktcKimlikNo = "0000000001",
            Status = EmploymentStatus.Active
        };
        var credentialService = new EmployeeCredentialService(
            new PasswordHasher<EmployeeCredential>());
        employee.Credential = credentialService.CreateInitial(employee);
        var now = DateTimeOffset.UtcNow;
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Employees
                (EmployeeId, SicilNo, FirstName, LastName, Email, KKTC_KimlikNo,
                 DepartmentId, ApplicationRoleId, Status, CreatedAt, UpdatedAt, RowVersion)
            VALUES
                ({employee.EmployeeId}, {employee.SicilNo}, {employee.FirstName}, {employee.LastName},
                 {employee.Email}, {employee.KktcKimlikNo}, {employee.DepartmentId},
                 {employee.ApplicationRoleId}, {(int)employee.Status}, {now}, {now}, {new byte[] { 1 }});
            """);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO EmployeeCredentials
                (EmployeeId, PasswordHash, MustChangePassword, PasswordChangedAt)
            VALUES
                ({employee.EmployeeId}, {employee.Credential.PasswordHash}, {true}, {(DateTimeOffset?)null});
            """);

        return new Fixture(
            connection,
            database,
            credentialService,
            new PasswordChangeService(
                database,
                credentialService,
                new AuditLogService(database)));
    }

    private static ClaimsPrincipal Principal(int employeeId) =>
        new(new ClaimsIdentity(
        [
            new Claim(UserClaimTypes.EmployeeId, employeeId.ToString()),
            new Claim(UserClaimTypes.MustChangePassword, bool.TrueString)
        ], "Test"));

    private sealed class Fixture(
        SqliteConnection connection,
        HumanResourcesDbContext database,
        EmployeeCredentialService credentialService,
        PasswordChangeService service) : IAsyncDisposable
    {
        public HumanResourcesDbContext Database { get; } = database;
        public EmployeeCredentialService CredentialService { get; } = credentialService;
        public PasswordChangeService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
