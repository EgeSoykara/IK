using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.Data.Sqlite;
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
    public async Task ReviewAsync_AcknowledgesCurrentValueAndWritesAuditRecord()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.ReviewAsync(
            ManagerPrincipal(),
            warningId: 1,
            carryOverDays: 30m,
            warningRowVersion: [],
            balanceRowVersion: [],
            actorEmployeeId: 1);

        Assert.False(result.CarryOverChanged);
        Assert.False(result.WithinLimit);
        var balance = await dbContext.LeaveBalances.SingleAsync();
        Assert.True(balance.CarryOverLimitWarningConfirmed);
        Assert.Equal("1", balance.CarryOverLimitWarningConfirmedBy);
        Assert.NotNull(balance.CarryOverLimitWarningConfirmedAt);
        var warning = await dbContext.LeaveCarryOverWarnings.SingleAsync();
        Assert.True(warning.IsAcknowledged);
        Assert.Equal("1", warning.AcknowledgedBy);
        Assert.NotNull(warning.AcknowledgedAt);
        var auditLog = await dbContext.AuditLogs.SingleAsync(item =>
            item.ActionType == AuditActionType.LeaveCarryOverUpdated);
        Assert.Equal(nameof(LeaveCarryOverWarning), auditLog.EntityName);
        Assert.Equal("1", auditLog.EntityId);
        Assert.Contains("Reviewed=true", auditLog.Details);
        Assert.DoesNotContain("Çalışan", auditLog.Details ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReviewAsync_UpdatesBalanceAndMovesWithinLimitWarningToAcknowledged()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.ReviewAsync(
            ManagerPrincipal(),
            warningId: 1,
            carryOverDays: 20m,
            warningRowVersion: [],
            balanceRowVersion: [],
            actorEmployeeId: 1);

        Assert.True(result.CarryOverChanged);
        Assert.True(result.WithinLimit);
        var balance = await dbContext.LeaveBalances.SingleAsync();
        Assert.Equal(20m, balance.CarryOverDays);
        Assert.Equal(50m, balance.RemainingDays);
        Assert.False(balance.CarryOverLimitWarningConfirmed);
        var warning = await dbContext.LeaveCarryOverWarnings.SingleAsync();
        Assert.True(warning.IsAcknowledged);
        Assert.Equal("1", warning.AcknowledgedBy);
        Assert.Equal(20m, warning.CarryOverDays);
        Assert.Equal(50m, warning.TotalDays);
        var auditLog = await dbContext.AuditLogs.SingleAsync(item =>
            item.ActionType == AuditActionType.LeaveCarryOverUpdated);
        Assert.Contains("CarryOverDays=30->20", auditLog.Details);
        Assert.Contains("WithinLimit=True", auditLog.Details);
    }

    [Fact]
    public async Task ReviewAsync_RejectsPrincipalWithoutBalancePermission()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext);
        var service = CreateService(dbContext);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ReviewAsync(
                principal,
                warningId: 1,
                carryOverDays: 20m,
                warningRowVersion: [],
                balanceRowVersion: [],
                actorEmployeeId: 1));

        Assert.Equal(30m, (await dbContext.LeaveBalances.SingleAsync()).CarryOverDays);
        Assert.False(await dbContext.AuditLogs.AnyAsync());
    }

    [Fact]
    public async Task ReviewAsync_RejectsAlreadyAcknowledgedWarning()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext, acknowledged: true);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReviewAsync(
                ManagerPrincipal(),
                warningId: 1,
                carryOverDays: 20m,
                warningRowVersion: [],
                balanceRowVersion: [],
                actorEmployeeId: 1));

        Assert.Contains("zaten incelendi", exception.Message);
        Assert.Equal(30m, (await dbContext.LeaveBalances.SingleAsync()).CarryOverDays);
    }

    [Fact]
    public async Task ReviewAsync_KeepsOverLimitDecisionAcknowledgedWithNewSnapshot()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.ReviewAsync(
            ManagerPrincipal(),
            warningId: 1,
            carryOverDays: 35m,
            warningRowVersion: [],
            balanceRowVersion: [],
            actorEmployeeId: 1);

        Assert.True(result.CarryOverChanged);
        Assert.False(result.WithinLimit);
        var balance = await dbContext.LeaveBalances.SingleAsync();
        Assert.Equal(35m, balance.CarryOverDays);
        Assert.Equal(65m, balance.RemainingDays);
        Assert.True(balance.CarryOverLimitWarningConfirmed);
        Assert.Equal("1", balance.CarryOverLimitWarningConfirmedBy);
        var warning = await dbContext.LeaveCarryOverWarnings.SingleAsync();
        Assert.Equal(35m, warning.CarryOverDays);
        Assert.Equal(65m, warning.TotalDays);
        Assert.True(warning.IsAcknowledged);
        Assert.NotNull(warning.AcknowledgedAt);
        Assert.Equal("1", warning.AcknowledgedBy);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(1.25)]
    public async Task ReviewAsync_RejectsInvalidDayAmounts(double value)
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext);
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReviewAsync(
                ManagerPrincipal(),
                warningId: 1,
                carryOverDays: (decimal)value,
                warningRowVersion: [],
                balanceRowVersion: [],
                actorEmployeeId: 1));
    }

    [Fact]
    public async Task ReviewAsync_UsesCorrectedUsageInsteadOfHistoricalAllocationCeiling()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(
            dbContext,
            usedDays: 20m,
            allocatedCarryOverDays: 20m);
        var service = CreateService(dbContext);

        await service.ReviewAsync(
            ManagerPrincipal(),
            warningId: 1,
            carryOverDays: 10m,
            warningRowVersion: [],
            balanceRowVersion: [],
            actorEmployeeId: 1);

        var balance = await dbContext.LeaveBalances.SingleAsync();
        Assert.Equal(10m, balance.CarryOverDays);
        Assert.Equal(20m, balance.UsedDays);
        Assert.Equal(20m, balance.RemainingDays);
        Assert.True((await dbContext.LeaveCarryOverWarnings.SingleAsync()).IsAcknowledged);
    }

    [Fact]
    public async Task ReviewAsync_PreservesCorrectedUsedDaysWithoutAllocationEquality()
    {
        await using var dbContext = CreateDbContext();
        await SeedWarningAsync(dbContext, usedDays: 5m);
        var service = CreateService(dbContext);

        await service.ReviewAsync(
            ManagerPrincipal(),
            warningId: 1,
            carryOverDays: 20m,
            warningRowVersion: [],
            balanceRowVersion: [],
            actorEmployeeId: 1);

        var balance = await dbContext.LeaveBalances.SingleAsync();
        Assert.Equal(5m, balance.UsedDays);
        Assert.Equal(45m, balance.RemainingDays);
        Assert.True((await dbContext.LeaveCarryOverWarnings.SingleAsync()).IsAcknowledged);
    }

    [Fact]
    public async Task ReviewAsync_AfterRelationalConcurrencyConflict_RetriesWithFreshContext()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation(
            "Latin1_General_100_BIN2",
            static (left, right) => string.CompareOrdinal(left, right));
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new HumanResourcesDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        var initialRowVersion = new byte[] { 1 };
        var now = DateTimeOffset.UtcNow;
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO LeaveTypes
                (LeaveTypeId, Name, AnnualQuota, CarryOverRule, MaxAccrualDays, EntitlementKind)
            VALUES
                (9001, {"İlişkisel Çakışma Test İzni"}, {30m}, {true}, {50m}, {(int)LeaveEntitlementKind.ServiceYears0To10});
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO LeaveBalances
                (BalanceId, EmployeeId, LeaveTypeId, Year, EntitledDays, CarryOverDays,
                 UsedDays, RemainingDays, CarryOverLimitWarningConfirmed,
                 CarryOverLimitWarningConfirmedAt, CarryOverLimitWarningConfirmedBy,
                 CreatedAt, UpdatedAt, RowVersion)
            VALUES
                (1, 1, 9001, 2026, {30m}, {30m}, {0m}, {60m}, {false}, NULL, NULL,
                 {now}, {now}, {initialRowVersion});
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO LeaveCarryOverWarnings
                (WarningId, BalanceId, EmployeeId, LeaveTypeId, Year, CarryOverDays,
                 EntitledDays, TotalDays, WarningLimitDays, IsAcknowledged, CreatedAt,
                 UpdatedAt, AcknowledgedAt, AcknowledgedBy, RowVersion)
            VALUES
                (1, 1, 1, 9001, 2026, {30m}, {30m}, {60m}, {50m}, {false}, {now},
                 {now}, NULL, NULL, {initialRowVersion});
            """);

        var dbContextFactory = new TestHumanResourcesDbContextFactory(options);
        var service = CreateService(dbContext, dbContextFactory);
        var staleWarningRowVersion = await dbContext.LeaveCarryOverWarnings
            .AsNoTracking()
            .Select(item => item.RowVersion)
            .SingleAsync();
        var staleBalanceRowVersion = await dbContext.LeaveBalances
            .AsNoTracking()
            .Select(item => item.RowVersion)
            .SingleAsync();
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE LeaveCarryOverWarnings SET RowVersion = X'02' WHERE WarningId = 1;");
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE LeaveBalances SET RowVersion = X'02' WHERE BalanceId = 1;");

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            service.ReviewAsync(
                ManagerPrincipal(),
                warningId: 1,
                carryOverDays: 20m,
                warningRowVersion: staleWarningRowVersion,
                balanceRowVersion: staleBalanceRowVersion,
                actorEmployeeId: 1));

        await using var refreshContext = await dbContextFactory.CreateDbContextAsync();
        var freshWarningRowVersion = await refreshContext.LeaveCarryOverWarnings
            .AsNoTracking()
            .Select(item => item.RowVersion)
            .SingleAsync();
        var freshBalanceRowVersion = await refreshContext.LeaveBalances
            .AsNoTracking()
            .Select(item => item.RowVersion)
            .SingleAsync();
        var result = await service.ReviewAsync(
            ManagerPrincipal(),
            warningId: 1,
            carryOverDays: 20m,
            warningRowVersion: freshWarningRowVersion,
            balanceRowVersion: freshBalanceRowVersion,
            actorEmployeeId: 1);

        Assert.True(result.CarryOverChanged);
        Assert.True(result.WithinLimit);
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(20m, await verificationContext.LeaveBalances
            .Select(item => item.CarryOverDays)
            .SingleAsync());
        Assert.True(await verificationContext.LeaveCarryOverWarnings
            .Select(item => item.IsAcknowledged)
            .SingleAsync());
        Assert.Equal(1, await verificationContext.AuditLogs.CountAsync());
        Assert.True(dbContextFactory.CreatedContextCount >= 4);
    }

    private static LeaveCarryOverWarningService CreateService(HumanResourcesDbContext dbContext)
    {
        var dbContextFactory = TestHumanResourcesDbContextFactory.From(dbContext);
        return CreateService(dbContext, dbContextFactory);
    }

    private static LeaveCarryOverWarningService CreateService(
        HumanResourcesDbContext dbContext,
        TestHumanResourcesDbContextFactory dbContextFactory)
    {
        return new LeaveCarryOverWarningService(
            dbContext,
            dbContextFactory,
            new PageAccessService(dbContextFactory));
    }

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
            Email = "employee.test@example.com",
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
        dbContext.ChangeTracker.Clear();
    }
}
