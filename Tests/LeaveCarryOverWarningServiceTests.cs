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

    [Fact]
    public async Task UpdateCarryOverDaysAsync_UpdatesBalanceAndRemovesResolvedWarning()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.UpdateCarryOverDaysAsync(
            ManagerPrincipal(),
            warningId: 1,
            carryOverDays: 20m,
            warningRowVersion: [],
            balanceRowVersion: [],
            actorUserId: "admin-user");

        Assert.True(result.Changed);
        Assert.True(result.WarningResolved);
        var balance = await dbContext.LeaveBalances.SingleAsync();
        Assert.Equal(20m, balance.CarryOverDays);
        Assert.Equal(50m, balance.RemainingDays);
        Assert.False(balance.CarryOverLimitWarningConfirmed);
        Assert.False(await dbContext.LeaveCarryOverWarnings.AnyAsync());
        var auditLog = await dbContext.AuditLogs.SingleAsync(item =>
            item.ActionType == AuditActionType.LeaveCarryOverUpdated);
        Assert.Contains("CarryOverDays=30->20", auditLog.Details);
        Assert.Contains("WarningResolved=True", auditLog.Details);
    }

    [Fact]
    public async Task UpdateCarryOverDaysAsync_RejectsPrincipalWithoutBalancePermission()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext);
        var service = CreateService(dbContext);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateCarryOverDaysAsync(
                principal,
                warningId: 1,
                carryOverDays: 20m,
                warningRowVersion: [],
                balanceRowVersion: [],
                actorUserId: "unauthorized-user"));

        Assert.Equal(30m, (await dbContext.LeaveBalances.SingleAsync()).CarryOverDays);
        Assert.False(await dbContext.AuditLogs.AnyAsync());
    }

    [Fact]
    public async Task UpdateCarryOverDaysAsync_KeepsOverLimitWarningPendingWithNewSnapshot()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext, acknowledged: true);
        var service = CreateService(dbContext);

        var result = await service.UpdateCarryOverDaysAsync(
            ManagerPrincipal(),
            warningId: 1,
            carryOverDays: 35m,
            warningRowVersion: [],
            balanceRowVersion: [],
            actorUserId: "admin-user");

        Assert.True(result.Changed);
        Assert.False(result.WarningResolved);
        var balance = await dbContext.LeaveBalances.SingleAsync();
        Assert.Equal(35m, balance.CarryOverDays);
        Assert.Equal(65m, balance.RemainingDays);
        Assert.True(balance.CarryOverLimitWarningConfirmed);
        Assert.Equal("admin-user", balance.CarryOverLimitWarningConfirmedBy);
        var warning = await dbContext.LeaveCarryOverWarnings.SingleAsync();
        Assert.Equal(35m, warning.CarryOverDays);
        Assert.Equal(65m, warning.TotalDays);
        Assert.False(warning.IsAcknowledged);
        Assert.Null(warning.AcknowledgedAt);
        Assert.Null(warning.AcknowledgedBy);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(1.25)]
    public async Task UpdateCarryOverDaysAsync_RejectsInvalidDayAmounts(double value)
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext);
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateCarryOverDaysAsync(
                ManagerPrincipal(),
                warningId: 1,
                carryOverDays: (decimal)value,
                warningRowVersion: [],
                balanceRowVersion: [],
                actorUserId: "admin-user"));
    }

    [Fact]
    public async Task UpdateCarryOverDaysAsync_RejectsValueBelowAllocatedCarryOver()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(
            dbContext,
            usedDays: 20m,
            allocatedCarryOverDays: 20m);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateCarryOverDaysAsync(
                ManagerPrincipal(),
                warningId: 1,
                carryOverDays: 10m,
                warningRowVersion: [],
                balanceRowVersion: [],
                actorUserId: "admin-user"));

        Assert.Contains("onaylı taleplerde kullanılmış devir", exception.Message);
        Assert.Equal(30m, (await dbContext.LeaveBalances.SingleAsync()).CarryOverDays);
    }

    [Fact]
    public async Task UpdateCarryOverDaysAsync_RejectsInconsistentAllocationTotals()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext, usedDays: 5m);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateCarryOverDaysAsync(
                ManagerPrincipal(),
                warningId: 1,
                carryOverDays: 20m,
                warningRowVersion: [],
                balanceRowVersion: [],
                actorUserId: "admin-user"));

        Assert.Contains("düşüm kaynakları tutarlı değil", exception.Message);
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

    private static async Task SeedWarningAsync(
        HumanResourcesDbContext dbContext,
        bool acknowledged = false,
        decimal usedDays = 0m,
        decimal allocatedCarryOverDays = 0m,
        decimal allocatedEntitlementDays = 0m)
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
            UsedDays = usedDays,
            RemainingDays = 60m - usedDays
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
            WarningLimitDays = 50m,
            IsAcknowledged = acknowledged,
            AcknowledgedAt = acknowledged ? DateTimeOffset.UtcNow : null,
            AcknowledgedBy = acknowledged ? "previous-admin" : null
        });
        if (allocatedCarryOverDays > 0m || allocatedEntitlementDays > 0m)
        {
            dbContext.LeaveRequests.Add(new LeaveRequest
            {
                RequestId = 1,
                EmployeeId = 1,
                Category = LeaveRequestCategory.AnnualLeave,
                LeaveTypeId = 1,
                RequestedDays = allocatedCarryOverDays + allocatedEntitlementDays,
                Reason = "Test allocation",
                CurrentStatus = LeaveRequestStatus.Approved
            });
            if (allocatedCarryOverDays > 0m)
            {
                dbContext.LeaveRequestBalanceAllocations.Add(new LeaveRequestBalanceAllocation
                {
                    AllocationId = 1,
                    RequestId = 1,
                    BalanceId = 1,
                    Source = LeaveBalanceAllocationSource.CarryOver,
                    Days = allocatedCarryOverDays
                });
            }

            if (allocatedEntitlementDays > 0m)
            {
                dbContext.LeaveRequestBalanceAllocations.Add(new LeaveRequestBalanceAllocation
                {
                    AllocationId = 2,
                    RequestId = 1,
                    BalanceId = 1,
                    Source = LeaveBalanceAllocationSource.Entitlement,
                    Days = allocatedEntitlementDays
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
