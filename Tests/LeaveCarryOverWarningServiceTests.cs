using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class LeaveCarryOverWarningServiceTests
{
    [Fact]
    public async Task AuthorizedQuery_RejectsPrincipalWithoutBalancePermission()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));

        Assert.Throws<UnauthorizedAccessException>(() => service.AuthorizedQuery(
            principal,
            new LeaveCarryOverWarningFilter(null, null, null, null)));
    }

    [Fact]
    public async Task AcknowledgeAsync_PersistsOperatorAndAuditRecord()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext);
        var service = CreateService(dbContext);

        await service.AcknowledgeAsync(
            ManagerPrincipal(),
            warningId: 1,
            rowVersion: [],
            actorUserId: "admin-user");

        var warning = await dbContext.LeaveCarryOverWarnings.SingleAsync();
        Assert.True(warning.IsAcknowledged);
        Assert.Equal("admin-user", warning.AcknowledgedBy);
        Assert.NotNull(warning.AcknowledgedAt);
        var auditLog = await dbContext.AuditLogs.SingleAsync(item =>
            item.ActionType == AuditActionType.LeaveCarryOverUpdated);
        Assert.Equal(nameof(LeaveCarryOverWarning), auditLog.EntityName);
        Assert.Equal("1", auditLog.EntityId);
        Assert.DoesNotContain("Çalışan", auditLog.Details ?? string.Empty, StringComparison.Ordinal);
    }

    private static LeaveCarryOverWarningService CreateService(HumanResourcesDbContext dbContext) =>
        new(
            dbContext,
            new PageAccessService(TestHumanResourcesDbContextFactory.From(dbContext)),
            new AuditLogService(dbContext));

    private static ClaimsPrincipal ManagerPrincipal() =>
        new(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "admin-user"),
                    new Claim(
                        PermissionClaimTypes.Permission,
                        PermissionNames.CanManageLeaveBalances)
                ],
                "Test"));

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HumanResourcesDbContext(options);
    }

    private static async Task SeedWarningAsync(HumanResourcesDbContext dbContext)
    {
        dbContext.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "İnsan Kaynakları"
        });
        dbContext.Employees.Add(new Employee
        {
            EmployeeId = 1,
            DepartmentId = 1,
            SicilNo = "S1",
            FirstName = "Çalışan",
            LastName = "Test",
            KktcKimlikNo = "1000000001",
            Status = EmploymentStatus.Active
        });
        dbContext.LeaveTypes.Add(new LeaveType
        {
            LeaveTypeId = 1,
            Name = "0-10 Yıllık Çalışan İzni",
            AnnualQuota = 30m,
            CarryOverRule = true,
            MaxAccrualDays = 50m,
            EntitlementKind = LeaveEntitlementKind.ServiceYears0To10
        });
        dbContext.LeaveBalances.Add(new LeaveBalance
        {
            BalanceId = 1,
            EmployeeId = 1,
            LeaveTypeId = 1,
            Year = 2026,
            EntitledDays = 30m,
            CarryOverDays = 30m,
            UsedDays = 0m,
            RemainingDays = 60m
        });
        dbContext.LeaveCarryOverWarnings.Add(new LeaveCarryOverWarning
        {
            WarningId = 1,
            BalanceId = 1,
            EmployeeId = 1,
            LeaveTypeId = 1,
            Year = 2026,
            EntitledDays = 30m,
            CarryOverDays = 30m,
            TotalDays = 60m,
            WarningLimitDays = 50m
        });
        await dbContext.SaveChangesAsync();
    }
}
