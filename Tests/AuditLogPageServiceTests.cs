using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class AuditLogPageServiceTests
{
    [Fact]
    public async Task GetPageAsync_ReturnsStablePagesBeyondPreviousFiftyRowLimit()
    {
        await using var dbContext = CreateDbContext();
        var actionDate = new DateTimeOffset(2026, 7, 14, 9, 0, 0, TimeSpan.Zero);

        dbContext.AuditLogs.AddRange(
            Enumerable.Range(1, 60).Select(id => new AuditLog
            {
                AuditLogId = id,
                ActorEmployeeId = 1,
                ActionType = AuditActionType.Login,
                EntityName = nameof(Employee),
                EntityId = id.ToString(),
                ActionDate = actionDate
            }));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var principal = AuditViewerPrincipal();
        var criteria = new AuditLogSearchCriteria(null, AuditActionType.Login, null, null, null);

        var firstPage = await service.GetPageAsync(principal, page: 0, pageSize: 25, criteria);
        var secondPage = await service.GetPageAsync(principal, page: 1, pageSize: 25, criteria);
        var thirdPage = await service.GetPageAsync(principal, page: 2, pageSize: 25, criteria);

        Assert.Equal(60, firstPage.TotalItems);
        Assert.Equal(60, secondPage.TotalItems);
        Assert.Equal(60, thirdPage.TotalItems);
        Assert.Equal(DescendingIds(36, 25), firstPage.Items.Select(log => log.AuditLogId));
        Assert.Equal(DescendingIds(11, 25), secondPage.Items.Select(log => log.AuditLogId));
        Assert.Equal(DescendingIds(1, 10), thirdPage.Items.Select(log => log.AuditLogId));
    }

    [Fact]
    public async Task SearchEmployeesAsync_ReturnsDistinctMatchingEmployeesInDisplayOrder()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Employees.AddRange(
            ActorEmployee(1, "Zeynep", "Akın", "S-001"),
            ActorEmployee(2, "Ahmet", "Yılmaz", "S-002"));
        dbContext.AuditLogs.AddRange(
            new AuditLog { ActorEmployeeId = 1, ActionType = AuditActionType.Login, EntityName = nameof(Employee), EntityId = "1" },
            new AuditLog { ActorEmployeeId = 2, ActionType = AuditActionType.Login, EntityName = nameof(Employee), EntityId = "2" },
            new AuditLog { ActorEmployeeId = 2, ActionType = AuditActionType.EmployeeUpdated, EntityName = nameof(Employee), EntityId = "3" });
        await dbContext.SaveChangesAsync();

        var employees = await CreateService(dbContext).SearchEmployeesAsync(AuditViewerPrincipal(), "Ah");

        var employee = Assert.Single(employees);
        Assert.Equal(2, employee.EmployeeId);
        Assert.Equal("Ahmet Yılmaz", employee.DisplayName);
        Assert.Contains("S-002", employee.Label);
    }

    [Fact]
    public async Task GetEmployeeOptionAsync_ReturnsExactCurrentEmployeeAndRejectsUnauthorized()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Employees.Add(ActorEmployee(4, "İK", "Yetkilisi", "IK-004"));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var employee = await service.GetEmployeeOptionAsync(AuditViewerPrincipal(), 4);

        Assert.NotNull(employee);
        Assert.Equal(4, employee.EmployeeId);
        Assert.Equal("İK Yetkilisi", employee.DisplayName);
        Assert.Equal("IK-004", employee.SicilNo);
        var unauthorized = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetEmployeeOptionAsync(unauthorized, 4));
    }

    [Fact]
    public async Task SearchEmployeesAsync_RejectsUnauthorizedAndHonorsCancellation()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var unauthorized = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SearchEmployeesAsync(unauthorized, null));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SearchEmployeesAsync(AuditViewerPrincipal(), null, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task AutocompleteAndTableReads_CreateIndependentContexts()
    {
        await using var database = CreateDbContext();
        database.AuditLogs.Add(new AuditLog
        {
            ActorEmployeeId = 1,
            ActionType = AuditActionType.Login,
            EntityName = nameof(Employee),
            EntityId = "1"
        });
        await database.SaveChangesAsync();
        var factory = TestHumanResourcesDbContextFactory.From(database);
        var service = new AuditLogPageService(factory, new PageAccessService(factory));
        var principal = AuditViewerPrincipal();

        await service.SearchEmployeesAsync(principal, "a");
        await service.GetPageAsync(
            principal, 0, 25,
            new AuditLogSearchCriteria(1, null, null, null, null));

        Assert.Equal(2, factory.CreatedContextCount);
    }

    [Fact]
    public async Task GetPageAsync_EmployeeFilterMatchesSelectedEmployeeExactly()
    {
        await using var dbContext = CreateDbContext();
        dbContext.AuditLogs.AddRange(
            new AuditLog { AuditLogId = 1, ActorEmployeeId = 1, ActionType = AuditActionType.Login, EntityName = nameof(Employee), EntityId = "1" },
            new AuditLog { AuditLogId = 2, ActorEmployeeId = 2, ActionType = AuditActionType.Login, EntityName = nameof(Employee), EntityId = "2" });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).GetPageAsync(
            AuditViewerPrincipal(), 0, 25,
            new AuditLogSearchCriteria(1, null, null, null, null));

        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, Assert.Single(result.Items).ActorEmployeeId);
    }

    [Fact]
    public async Task GetPageAsync_RejectsPrincipalWithoutAuditPermission()
    {
        await using var dbContext = CreateDbContext();
        dbContext.AuditLogs.Add(
            new AuditLog
            {
                ActorEmployeeId = 1,
                ActionType = AuditActionType.Login,
                EntityName = nameof(Employee),
                EntityId = "1"
            });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        var unauthorized = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetPageAsync(
                unauthorized,
                page: 0,
                pageSize: 25,
                new AuditLogSearchCriteria(1, null, null, null, null)));
    }

    [Fact]
    public async Task GetPageAsync_WithoutCriteriaDoesNotLoadAuditRows()
    {
        await using var dbContext = CreateDbContext();
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorEmployeeId = 1,
            ActionType = AuditActionType.Login,
            EntityName = nameof(Employee),
            EntityId = "1"
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).GetPageAsync(
            AuditViewerPrincipal(),
            page: 0,
            pageSize: 25,
            new AuditLogSearchCriteria(null, null, null, null, null));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalItems);
    }

    [Fact]
    public async Task GetPageAsync_AppliesEmployeeActionDateAndDetailFiltersBeforePaging()
    {
        await using var dbContext = CreateDbContext();
        dbContext.AuditLogs.AddRange(
            new AuditLog { AuditLogId = 1, ActorEmployeeId = 1, ActionType = AuditActionType.LeaveBalanceUpdated, EntityName = nameof(LeaveBalance), EntityId = "1", ActionDate = new DateTimeOffset(2026, 8, 4, 10, 0, 0, TimeSpan.Zero), Details = "UsedDays=1->2" },
            new AuditLog { AuditLogId = 2, ActorEmployeeId = 1, ActionType = AuditActionType.Login, EntityName = nameof(Employee), EntityId = "1", ActionDate = new DateTimeOffset(2026, 8, 4, 11, 0, 0, TimeSpan.Zero) },
            new AuditLog { AuditLogId = 3, ActorEmployeeId = 2, ActionType = AuditActionType.LeaveBalanceUpdated, EntityName = nameof(LeaveBalance), EntityId = "2", ActionDate = new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero), Details = "UsedDays=1->2" });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).GetPageAsync(
            AuditViewerPrincipal(),
            0,
            25,
            new AuditLogSearchCriteria(
                1,
                AuditActionType.LeaveBalanceUpdated,
                new DateOnly(2026, 8, 4),
                new DateOnly(2026, 8, 4),
                "UsedDays"));

        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, Assert.Single(result.Items).AuditLogId);
    }

    [Fact]
    public async Task GetPageAsync_LabelsSystemAndDeletedEmployeeActorsWithoutUsernameFallback()
    {
        await using var dbContext = CreateDbContext();
        dbContext.AuditLogs.AddRange(
            new AuditLog
            {
                AuditLogId = 1,
                ActorEmployeeId = 999,
                ActionType = AuditActionType.Login,
                EntityName = nameof(Employee),
                EntityId = "999"
            },
            new AuditLog
            {
                AuditLogId = 2,
                SystemActorKey = DailyLeaveEntitlementWorker.SystemActor,
                ActionType = AuditActionType.Login,
                EntityName = nameof(Employee),
                EntityId = "system"
            },
            new AuditLog
            {
                AuditLogId = 3,
                SystemActorKey = SystemActorKeys.InitialConfiguration,
                ActionType = AuditActionType.Login,
                EntityName = nameof(Employee),
                EntityId = "initial"
            });

        dbContext.AuditLogs.Add(
            new AuditLog
            {
                AuditLogId = 4,
                SystemActorKey = SystemActorKeys.ManagerRoleBackfill,
                ActionType = AuditActionType.Login,
                EntityName = nameof(Employee),
                EntityId = "4",
                Details = "ApplicationRoleId=1->2"
            });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).GetPageAsync(
            AuditViewerPrincipal(),
            0,
            25,
            new AuditLogSearchCriteria(null, AuditActionType.Login, null, null, null));

        Assert.Contains(result.Items, item => item.ActorDisplayName == "Silinmiş çalışan (#999)");
        Assert.Contains(result.Items, item => item.ActorDisplayName == "Günlük İzin Otomasyonu");
        Assert.Contains(result.Items, item => item.ActorDisplayName == "İlk Kurulum");
        Assert.Contains(result.Items, item => item.ActorDisplayName == "Yönetici Rolü Geçişi");
    }

    [Fact]
    public async Task AuditLogService_RequiresValidEmployeeOrNonEmptySystemActor()
    {
        using var dbContext = CreateDbContext();
        var service = new AuditLogService(dbContext);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.AppendAsync(AuditActionType.Login, nameof(Employee), "1", 0, null));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AppendSystemAsync(AuditActionType.Login, nameof(Employee), "1", " ", null));

        await service.AppendAsync(AuditActionType.Login, nameof(Employee), "1", 1, null);
        await service.AppendSystemAsync(
            AuditActionType.Login,
            nameof(Employee),
            "system",
            DailyLeaveEntitlementWorker.SystemActor,
            null);

        Assert.Contains(dbContext.AuditLogs.Local, item => item.ActorEmployeeId == 1 && item.SystemActorKey == null);
        Assert.Contains(dbContext.AuditLogs.Local, item => item.ActorEmployeeId == null && item.SystemActorKey == DailyLeaveEntitlementWorker.SystemActor);
    }

    [Fact]
    public void FormatDetails_UsesPlainTurkishLabelsAndHidesUnnecessaryZeros()
    {
        var result = AuditLogPresentation.FormatDetails(new AuditLog
        {
            Details = "DisplayName=Yönetici; UsedDays=2.0->3.5; ConfirmedOverLimit=false; Status=HumanResourcesReview"
        });

        Assert.Equal(
            "Görünen ad: Yönetici · Kullanılan gün: 2 gün → 3,5 gün · Sınır aşımı onayı: Hayır · Durum: İK onayı bekleniyor",
            result);
    }

    [Fact]
    public void FormatDetails_TranslatesResponsibilityRoleTransition()
    {
        var result = AuditLogPresentation.FormatDetails(new AuditLog
        {
            Details = "ApplicationRoleId=1->2; Source=ManagerDelegationActivated; HasManagementResponsibility=true"
        });

        Assert.Equal(
            "Uygulama rolü: Çalışan → Yönetici · Değişiklik kaynağı: Vekâlet başlangıcı · Yönetim sorumluluğu var: Evet",
            result);
    }

    private static IEnumerable<long> DescendingIds(int start, int count)
    {
        return Enumerable.Range(start, count).Reverse().Select(id => (long)id);
    }

    private static Employee ActorEmployee(
        int employeeId,
        string firstName,
        string lastName,
        string sicilNo) =>
        new()
        {
            EmployeeId = employeeId,
            FirstName = firstName,
            LastName = lastName,
            SicilNo = sicilNo,
            KktcKimlikNo = employeeId.ToString("D10")
        };

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HumanResourcesDbContext(options);
    }

    private static AuditLogPageService CreateService(
        HumanResourcesDbContext dbContext) =>
        new(
            TestHumanResourcesDbContextFactory.From(dbContext),
            new PageAccessService(TestHumanResourcesDbContextFactory.From(dbContext)));

    private static ClaimsPrincipal AuditViewerPrincipal() =>
        new(
            new ClaimsIdentity(
                [
                    new Claim(
                        PermissionClaimTypes.Permission,
                        PermissionNames.CanViewAuditLogs)
                ],
                "Test"));
}
