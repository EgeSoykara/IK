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

        var service = new AuditLogPageService(dbContext);

        var firstPage = await service.GetPageAsync(page: 0, pageSize: 25);
        var secondPage = await service.GetPageAsync(page: 1, pageSize: 25);
        var thirdPage = await service.GetPageAsync(page: 2, pageSize: 25);

        Assert.Equal(60, firstPage.TotalItems);
        Assert.Equal(60, secondPage.TotalItems);
        Assert.Equal(60, thirdPage.TotalItems);
        Assert.Equal(DescendingIds(36, 25), firstPage.Items.Select(log => log.AuditLogId));
        Assert.Equal(DescendingIds(11, 25), secondPage.Items.Select(log => log.AuditLogId));
        Assert.Equal(DescendingIds(1, 10), thirdPage.Items.Select(log => log.AuditLogId));
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
}
