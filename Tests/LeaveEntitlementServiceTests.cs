using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IK.Web.Tests;

public sealed class LeaveEntitlementServiceTests
{
    private readonly LeaveEntitlementService entitlementService = new();

    [Fact]
    public void Calculate_ProrationSplitsMarchTenthAnniversaryAcrossThreeAndNineMonths()
    {
        var employee = Employee(
            id: 1,
            departmentId: 1,
            startDate: new DateTime(2016, 3, 15),
            gender: EmployeeGender.Female);

        var firstTier = entitlementService.Calculate(
            employee,
            LeaveType(1, LeaveEntitlementKind.ServiceYears0To10),
            2026);
        var secondTier = entitlementService.Calculate(
            employee,
            LeaveType(2, LeaveEntitlementKind.ServiceYears10To20),
            2026);

        Assert.True(firstTier.Eligible);
        Assert.Equal(7.5m, firstTier.EntitledDays);
        Assert.True(secondTier.Eligible);
        Assert.Equal(22.5m, secondTier.EntitledDays);
    }

    [Fact]
    public void Calculate_ProrationSplitsTwentiethAnniversaryAndKeepsThirtyPlusInTopTier()
    {
        var transitionEmployee = Employee(
            id: 1,
            departmentId: 1,
            startDate: new DateTime(2006, 6, 15),
            gender: EmployeeGender.Male);
        var secondTier = entitlementService.Calculate(
            transitionEmployee,
            LeaveType(1, LeaveEntitlementKind.ServiceYears10To20),
            2026);
        var topTier = entitlementService.Calculate(
            transitionEmployee,
            LeaveType(2, LeaveEntitlementKind.ServiceYears20Plus),
            2026);

        Assert.Equal(15m, secondTier.EntitledDays);
        Assert.Equal(15m, topTier.EntitledDays);

        var thirtyPlusEmployee = Employee(
            id: 2,
            departmentId: 1,
            startDate: new DateTime(1990, 5, 1),
            gender: EmployeeGender.Male);
        var thirtyPlus = entitlementService.Calculate(
            thirtyPlusEmployee,
            LeaveType(2, LeaveEntitlementKind.ServiceYears20Plus),
            2026);

        Assert.True(thirtyPlus.Eligible);
        Assert.Equal(30m, thirtyPlus.EntitledDays);
    }

    [Fact]
    public void Calculate_AppliesGenderAndMissingStartDateRulesWithoutFallback()
    {
        var female = Employee(1, 1, new DateTime(2020, 1, 1), EmployeeGender.Female);
        var male = Employee(2, 1, new DateTime(2020, 1, 1), EmployeeGender.Male);
        var pregnancy = LeaveType(1, LeaveEntitlementKind.FemaleEmployees);
        var sickness = LeaveType(2, LeaveEntitlementKind.AllEmployees);

        Assert.True(entitlementService.Calculate(female, pregnancy, 2026).Eligible);
        Assert.False(entitlementService.Calculate(male, pregnancy, 2026).Eligible);
        Assert.True(entitlementService.Calculate(male, sickness, 2026).Eligible);

        var missingStartDate = Employee(3, 1, null, EmployeeGender.Female);
        var evaluation = entitlementService.Calculate(
            missingStartDate,
            LeaveType(3, LeaveEntitlementKind.ServiceYears0To10),
            2026);

        Assert.False(evaluation.Eligible);
        Assert.True(evaluation.MissingStartDate);
        Assert.Equal(0m, evaluation.EntitledDays);
    }

    [Fact]
    public async Task AutomaticAssignment_IsIdempotentAndPreservesFiftyDayWarning()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Employees.Add(
            Employee(1, 1, new DateTime(2018, 1, 1), EmployeeGender.Female));
        dbContext.LeaveTypes.Add(
            new LeaveType
            {
                LeaveTypeId = 10,
                Name = "Test Hastalık",
                AnnualQuota = 30m,
                CarryOverRule = true,
                MaxAccrualDays = 50m,
                EntitlementKind = LeaveEntitlementKind.AllEmployees
            });
        dbContext.LeaveBalances.Add(
            new LeaveBalance
            {
                EmployeeId = 1,
                LeaveTypeId = 10,
                Year = 2025,
                EntitledDays = 30m,
                RemainingDays = 30m
            });
        await dbContext.SaveChangesAsync();
        var service = CreateBalanceService(dbContext);

        var first = await service.AssignAutomaticAnnualEntitlementsAsync(2026, "worker");
        var second = await service.AssignAutomaticAnnualEntitlementsAsync(2026, "worker");

        Assert.Equal(1, first.CreatedCount);
        Assert.Equal(1, first.WarningCount);
        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(1, second.ExistingCount);
        var balance = await dbContext.LeaveBalances.SingleAsync(item => item.Year == 2026);
        Assert.Equal(60m, balance.RemainingDays);
        Assert.True(balance.CarryOverLimitWarningConfirmed);
        Assert.Equal("worker", balance.CarryOverLimitWarningConfirmedBy);
    }

    [Fact]
    public async Task AutomaticAssignment_CarriesServiceTierRemainderOnceAcrossTransition()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Employees.Add(
            Employee(1, 1, new DateTime(2016, 3, 15), EmployeeGender.Female));
        dbContext.LeaveTypes.AddRange(
            LeaveType(20, LeaveEntitlementKind.ServiceYears0To10),
            LeaveType(21, LeaveEntitlementKind.ServiceYears10To20));
        dbContext.LeaveBalances.Add(
            new LeaveBalance
            {
                EmployeeId = 1,
                LeaveTypeId = 20,
                Year = 2025,
                EntitledDays = 30m,
                RemainingDays = 12m
            });
        await dbContext.SaveChangesAsync();
        var service = CreateBalanceService(dbContext);

        var result = await service.AssignAutomaticAnnualEntitlementsAsync(2026, "worker");

        Assert.Equal(2, result.CreatedCount);
        var balances = await dbContext.LeaveBalances
            .Where(item => item.Year == 2026)
            .OrderBy(item => item.LeaveTypeId)
            .ToArrayAsync();
        Assert.Equal(12m, balances[0].CarryOverDays);
        Assert.Equal(19.5m, balances[0].RemainingDays);
        Assert.Equal(0m, balances[1].CarryOverDays);
        Assert.Equal(22.5m, balances[1].RemainingDays);
        Assert.Equal(42m, balances.Sum(item => item.RemainingDays));
    }

    [Fact]
    public async Task ManualAssignment_DepartmentScopeTargetsOnlyEligibleActiveEmployees()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Employees.AddRange(
            Employee(1, 4, new DateTime(2020, 1, 1), EmployeeGender.Female),
            Employee(2, 4, new DateTime(2020, 1, 1), EmployeeGender.Male),
            Employee(
                3,
                4,
                new DateTime(2020, 1, 1),
                EmployeeGender.Female,
                EmploymentStatus.Passive),
            Employee(4, 5, new DateTime(2020, 1, 1), EmployeeGender.Female));
        dbContext.LeaveTypes.Add(
            new LeaveType
            {
                LeaveTypeId = 11,
                Name = "Test Hamilelik",
                AnnualQuota = 30m,
                CarryOverRule = false,
                MaxAccrualDays = 50m,
                EntitlementKind = LeaveEntitlementKind.FemaleEmployees
            });
        await dbContext.SaveChangesAsync();
        var service = CreateBalanceService(dbContext);

        var result = await service.AssignManualAsync(
            ManagerPrincipal(),
            new LeaveBalanceAssignmentRequest(
                LeaveBalanceTargetScope.Department,
                EmployeeId: null,
                DepartmentId: 4,
                LeaveTypeId: 11,
                Year: 2026),
            "admin");

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.AssignedCount);
        Assert.Equal(1, result.SkippedCount);
        var balance = await dbContext.LeaveBalances.SingleAsync();
        Assert.Equal(1, balance.EmployeeId);
        Assert.Equal(30m, balance.EntitledDays);
    }

    [Fact]
    public async Task ManualAssignment_RejectsPrincipalWithoutManagementPermission()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateBalanceService(dbContext);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.AssignManualAsync(
                new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test")),
                new LeaveBalanceAssignmentRequest(
                    LeaveBalanceTargetScope.AllEmployees,
                    EmployeeId: null,
                    DepartmentId: null,
                    LeaveTypeId: 1,
                    Year: 2026),
                "unauthorized"));
    }

    [Fact]
    public async Task Update_RejectsEmployeeWhoIsIneligibleForSelectedLeaveType()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Employees.Add(
            Employee(1, 1, new DateTime(2020, 1, 1), EmployeeGender.Male));
        dbContext.LeaveTypes.AddRange(
            LeaveType(30, LeaveEntitlementKind.AllEmployees),
            LeaveType(31, LeaveEntitlementKind.FemaleEmployees));
        dbContext.LeaveBalances.Add(
            new LeaveBalance
            {
                BalanceId = 40,
                EmployeeId = 1,
                LeaveTypeId = 30,
                Year = 2026,
                EntitledDays = 30m,
                RemainingDays = 30m
            });
        await dbContext.SaveChangesAsync();
        var service = CreateBalanceService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(
                ManagerPrincipal(),
                balanceId: 40,
                employeeId: 1,
                leaveTypeId: 31,
                year: 2026,
                entitledDays: 30m,
                carryOverDays: 0m,
                rowVersion: [],
                actorUserId: "admin"));
    }

    [Fact]
    public async Task Update_RejectsCarryOverOnSecondServiceTierInTransitionYear()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Employees.Add(
            Employee(1, 1, new DateTime(2016, 3, 15), EmployeeGender.Female));
        dbContext.LeaveTypes.AddRange(
            LeaveType(40, LeaveEntitlementKind.ServiceYears0To10),
            LeaveType(41, LeaveEntitlementKind.ServiceYears10To20));
        dbContext.LeaveBalances.AddRange(
            new LeaveBalance
            {
                BalanceId = 50,
                EmployeeId = 1,
                LeaveTypeId = 40,
                Year = 2026,
                EntitledDays = 7.5m,
                CarryOverDays = 12m,
                RemainingDays = 19.5m
            },
            new LeaveBalance
            {
                BalanceId = 51,
                EmployeeId = 1,
                LeaveTypeId = 41,
                Year = 2026,
                EntitledDays = 22.5m,
                RemainingDays = 22.5m
            });
        await dbContext.SaveChangesAsync();
        var service = CreateBalanceService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(
                ManagerPrincipal(),
                balanceId: 51,
                employeeId: 1,
                leaveTypeId: 41,
                year: 2026,
                entitledDays: 22.5m,
                carryOverDays: 8m,
                rowVersion: [],
                actorUserId: "admin"));
    }

    [Fact]
    public async Task Worker_DisabledConfigurationAndCancellationFailClosed()
    {
        var disabledWorker = CreateWorker(
            new ImmediateTimeProvider(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)),
            enabled: false);
        var disabledAssignments = 0;

        await disabledWorker.RunAsync(
            (year, cancellationToken) =>
            {
                disabledAssignments++;
                return Task.FromResult(EmptyResult(year));
            },
            CancellationToken.None);

        Assert.Equal(0, disabledAssignments);

        var cancelledWorker = CreateWorker(
            new ImmediateTimeProvider(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelledAssignments = 0;

        await cancelledWorker.RunAsync(
            (year, cancellationToken) =>
            {
                cancelledAssignments++;
                return Task.FromResult(EmptyResult(year));
            },
            cancellation.Token);

        Assert.Equal(0, cancelledAssignments);
    }

    [Fact]
    public async Task Worker_RetriesFailureAndSchedulesNextRunForJanuaryFirst()
    {
        var timeProvider = new ImmediateTimeProvider(
            new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
        var worker = CreateWorker(timeProvider, retryDelayMinutes: 7);
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;

        await worker.RunAsync(
            (year, cancellationToken) =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new InvalidOperationException("transient");
                }

                cancellation.Cancel();
                return Task.FromResult(EmptyResult(year));
            },
            cancellation.Token);

        Assert.Equal(2, attempts);
        Assert.Contains(TimeSpan.FromMinutes(7), timeProvider.RequestedDelays);
        Assert.Equal(
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            worker.CalculateNextRun(2026));
    }

    private static LeaveBalanceService CreateBalanceService(
        HumanResourcesDbContext dbContext) =>
        new(
            dbContext,
            new AuditLogService(dbContext),
            new LeaveEntitlementService(),
            new PageAccessService(TestHumanResourcesDbContextFactory.From(dbContext)));

    private static LeaveType LeaveType(int id, LeaveEntitlementKind entitlementKind) =>
        new()
        {
            LeaveTypeId = id,
            Name = $"İzin {id}",
            AnnualQuota = 30m,
            CarryOverRule = true,
            MaxAccrualDays = 50m,
            EntitlementKind = entitlementKind
        };

    private static Employee Employee(
        int id,
        int departmentId,
        DateTime? startDate,
        EmployeeGender? gender,
        EmploymentStatus status = EmploymentStatus.Active) =>
        new()
        {
            EmployeeId = id,
            DepartmentId = departmentId,
            SicilNo = $"S{id}",
            FirstName = $"Çalışan{id}",
            LastName = "Test",
            KktcKimlikNo = id.ToString("D10"),
            StartDate = startDate,
            Gender = gender,
            Status = status
        };

    private static ClaimsPrincipal ManagerPrincipal() =>
        new(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, "admin"),
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

    private static AnnualLeaveEntitlementWorker CreateWorker(
        TimeProvider timeProvider,
        bool enabled = true,
        int retryDelayMinutes = 30) =>
        new(
            new ThrowingScopeFactory(),
            timeProvider,
            Options.Create(
                new AnnualLeaveEntitlementWorkerOptions
                {
                    Enabled = enabled,
                    RetryDelayMinutes = retryDelayMinutes
                }),
            NullLogger<AnnualLeaveEntitlementWorker>.Instance);

    private static AnnualLeaveEntitlementResult EmptyResult(int year) =>
        new(year, 0, 0, 0, 0, 0);

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Test assignment delegate must bypass DI scope creation.");
    }

    private sealed class ImmediateTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public List<TimeSpan> RequestedDelays { get; } = [];

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            RequestedDelays.Add(dueTime);
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return NoopTimer.Instance;
        }
    }

    private sealed class NoopTimer : ITimer
    {
        public static NoopTimer Instance { get; } = new();

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
