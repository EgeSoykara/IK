using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class EmployeeTerminationServiceTests
{
    [Fact]
    public async Task CreateAsync_MarksEmployeePassiveAndWritesSpecificAuditAtomically()
    {
        await using var database = CreateDbContext();
        await SeedEmployeeAsync(database, rowVersion: [1]);
        var service = CreateService(database);

        await service.SaveAsync(
            ManagerPrincipal(),
            new EmployeeTerminationCommand(null, 1, [1], new DateOnly(2026, 8, 7), "İstifa", "Devir tamamlandı"));

        await using var verification = CreateSiblingContext(database);
        var employee = await verification.Employees.SingleAsync();
        var termination = await verification.EmployeeTerminations.SingleAsync();
        var audit = await verification.AuditLogs.SingleAsync();
        Assert.Equal(EmploymentStatus.Passive, employee.Status);
        Assert.Equal("İstifa", termination.Reason);
        Assert.Equal(AuditActionType.EmployeeTerminationCreated, audit.ActionType);
        Assert.Contains("Status=Passive", audit.Details);
    }

    [Fact]
    public async Task Reads_AreAuthorizedServerPagedFilteredAndBounded()
    {
        await using var database = CreateDbContext();
        database.Departments.Add(new Department { DepartmentId = 1, DepartmentName = "İK" });
        for (var id = 1; id <= 30; id++)
        {
            var employee = Employee(id, id <= 2 ? EmploymentStatus.Active : EmploymentStatus.Passive, [1]);
            database.Employees.Add(employee);
            if (id > 2)
            {
                database.EmployeeTerminations.Add(new EmployeeTermination
                {
                    EmployeeId = id,
                    TerminationDate = new DateOnly(2026, 7, Math.Min(id, 28)),
                    Reason = id % 2 == 0 ? "Emeklilik" : "İstifa"
                });
            }
        }
        await database.SaveChangesAsync();
        var service = CreateService(database);

        var page = await service.GetPageAsync(
            ManagerPrincipal(), 1, 10,
            new EmployeeTerminationSearchCriteria(null, null, null, null));
        var filtered = await service.GetPageAsync(
            ManagerPrincipal(), 0, 25,
            new EmployeeTerminationSearchCriteria(null, "Emeklilik", null, null));
        var eligible = await service.SearchEligibleEmployeesAsync(ManagerPrincipal(), null, limit: 1);

        Assert.Equal(28, page.TotalItems);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(14, filtered.TotalItems);
        Assert.Single(eligible);
        Assert.All(eligible, employee => Assert.Equal(EmploymentStatus.Active, employee.Status));
    }

    [Fact]
    public async Task EveryReadAndMutationRejectsUnauthorizedPrincipal()
    {
        await using var database = CreateDbContext();
        await SeedEmployeeAsync(database, rowVersion: [1]);
        var service = CreateService(database);
        var unauthorized = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test"));
        var criteria = new EmployeeTerminationSearchCriteria(null, null, null, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetPageAsync(unauthorized, 0, 25, criteria));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(unauthorized, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SearchEligibleEmployeesAsync(unauthorized, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SearchRecordedEmployeesAsync(unauthorized, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SearchReasonsAsync(unauthorized, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(
            unauthorized,
            new EmployeeTerminationCommand(null, 1, [1], new DateOnly(2026, 8, 7), "İstifa", null)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(unauthorized, 1, [1]));
    }

    [Fact]
    public async Task Update_WithStaleEmployeeVersionFailsWithoutWritingAudit()
    {
        await using var database = CreateDbContext();
        await SeedEmployeeAsync(database, EmploymentStatus.Passive, [1], withTermination: true);
        await using (var concurrent = CreateSiblingContext(database))
        {
            var employee = await concurrent.Employees.SingleAsync();
            employee.RowVersion = [2];
            await concurrent.SaveChangesAsync();
        }
        var service = CreateService(database);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
            ManagerPrincipal(),
            new EmployeeTerminationCommand(1, 1, [1], new DateOnly(2026, 8, 8), "Emeklilik", null)));

        Assert.Contains("başka bir kullanıcı", exception.Message);
        await using var verification = CreateSiblingContext(database);
        Assert.Empty(await verification.AuditLogs.ToListAsync());
        Assert.Equal(new DateOnly(2026, 8, 7), (await verification.EmployeeTerminations.SingleAsync()).TerminationDate);
    }

    [Fact]
    public async Task Delete_PreservesPassiveEmployeeAndWritesDeleteAudit()
    {
        await using var database = CreateDbContext();
        await SeedEmployeeAsync(database, EmploymentStatus.Passive, [1], withTermination: true);
        var service = CreateService(database);

        await service.DeleteAsync(ManagerPrincipal(), 1, [1]);

        await using var verification = CreateSiblingContext(database);
        Assert.Empty(await verification.EmployeeTerminations.ToListAsync());
        Assert.Equal(EmploymentStatus.Passive, (await verification.Employees.SingleAsync()).Status);
        Assert.Equal(AuditActionType.EmployeeTerminationDeleted, (await verification.AuditLogs.SingleAsync()).ActionType);
    }

    [Fact]
    public async Task Update_ChangesValuesAndWritesSpecificUpdateAudit()
    {
        await using var database = CreateDbContext();
        await SeedEmployeeAsync(database, EmploymentStatus.Passive, [1], withTermination: true);
        var service = CreateService(database);

        await service.SaveAsync(
            ManagerPrincipal(),
            new EmployeeTerminationCommand(
                1, 1, [1], new DateOnly(2026, 8, 10), "Emeklilik", "Güncellendi"));

        await using var verification = CreateSiblingContext(database);
        var termination = await verification.EmployeeTerminations.SingleAsync();
        Assert.Equal(new DateOnly(2026, 8, 10), termination.TerminationDate);
        Assert.Equal("Emeklilik", termination.Reason);
        Assert.Equal("Güncellendi", termination.Description);
        Assert.Equal(AuditActionType.EmployeeTerminationUpdated, (await verification.AuditLogs.SingleAsync()).ActionType);
    }

    private static async Task SeedEmployeeAsync(
        HumanResourcesDbContext database,
        EmploymentStatus status = EmploymentStatus.Active,
        byte[]? rowVersion = null,
        bool withTermination = false)
    {
        database.Departments.Add(new Department { DepartmentId = 1, DepartmentName = "İK" });
        database.Employees.Add(Employee(1, status, rowVersion ?? [1]));
        if (withTermination)
        {
            database.EmployeeTerminations.Add(new EmployeeTermination
            {
                EmployeeTerminationId = 1,
                EmployeeId = 1,
                TerminationDate = new DateOnly(2026, 8, 7),
                Reason = "İstifa"
            });
        }
        await database.SaveChangesAsync();
    }

    private static Employee Employee(int id, EmploymentStatus status, byte[] rowVersion) => new()
    {
        EmployeeId = id,
        DepartmentId = 1,
        SicilNo = $"S{id:000}",
        FirstName = $"Çalışan{id}",
        LastName = "Test",
        KktcKimlikNo = id.ToString("0000000000"),
        StartDate = new DateTime(2020, 1, 1),
        Status = status,
        RowVersion = rowVersion
    };

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HumanResourcesDbContext(options);
    }

    private static HumanResourcesDbContext CreateSiblingContext(HumanResourcesDbContext database) =>
        TestHumanResourcesDbContextFactory.From(database).CreateDbContext();

    private static EmployeeTerminationService CreateService(HumanResourcesDbContext database)
    {
        var factory = TestHumanResourcesDbContextFactory.From(database);
        return new EmployeeTerminationService(factory, new PageAccessService(factory));
    }

    private static ClaimsPrincipal ManagerPrincipal() => new(
        new ClaimsIdentity(
            [
                new Claim(PermissionClaimTypes.Permission, PermissionNames.CanCreateNewEmployee),
                new Claim(ClaimTypes.Name, "manager"),
                new Claim(UserClaimTypes.EmployeeId, "99")
            ],
            "Test"));
}
