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
                UserId = $"user-{id}",
                ActionType = AuditActionType.Login,
                EntityName = nameof(Employee),
                EntityId = id.ToString(),
                ActionDate = actionDate
            }));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var principal = AuditViewerPrincipal();

        var firstPage = await service.GetPageAsync(principal, page: 0, pageSize: 25);
        var secondPage = await service.GetPageAsync(principal, page: 1, pageSize: 25);
        var thirdPage = await service.GetPageAsync(principal, page: 2, pageSize: 25);

        Assert.Equal(60, firstPage.TotalItems);
        Assert.Equal(60, secondPage.TotalItems);
        Assert.Equal(60, thirdPage.TotalItems);
        Assert.Equal(DescendingIds(36, 25), firstPage.Items.Select(log => log.AuditLogId));
        Assert.Equal(DescendingIds(11, 25), secondPage.Items.Select(log => log.AuditLogId));
        Assert.Equal(DescendingIds(1, 10), thirdPage.Items.Select(log => log.AuditLogId));
    }

    [Fact]
    public async Task GetPageAsync_RejectsPrincipalWithoutAuditPermission()
    {
        await using var dbContext = CreateDbContext();
        dbContext.AuditLogs.Add(
            new AuditLog
            {
                UserId = "admin",
                ActionType = AuditActionType.Login,
                EntityName = nameof(Employee),
                EntityId = "1"
            });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        var unauthorized = new ClaimsPrincipal(
            new ClaimsIdentity(authenticationType: "Test"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetPageAsync(unauthorized, page: 0, pageSize: 25));
    }

    private static IEnumerable<long> DescendingIds(int start, int count)
    {
        return Enumerable.Range(start, count).Reverse().Select(id => (long)id);
    }

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
            dbContext,
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
