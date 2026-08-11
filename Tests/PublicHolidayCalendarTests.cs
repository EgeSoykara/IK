using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class PublicHolidayCalendarTests
{
    [Fact]
    public async Task GetDatesAsync_UsesFactoryForPreviewAndCallerContextForTransaction()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase($"public-holiday-calendar-{Guid.NewGuid():N}")
            .Options;
        await using var transactionContext = new HumanResourcesDbContext(options);
        transactionContext.PublicHolidays.Add(new PublicHoliday
        {
            Date = new DateOnly(2026, 8, 11),
            Name = "Doğrulama Tatili"
        });
        await transactionContext.SaveChangesAsync();

        var factory = TestHumanResourcesDbContextFactory.From(transactionContext);
        var calendar = new PublicHolidayCalendar(factory);

        var previewDates = await calendar.GetDatesAsync(
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 12));
        Assert.Contains(new DateOnly(2026, 8, 11), previewDates);
        Assert.Equal(1, factory.CreatedContextCount);

        var transactionalDates = await calendar.GetDatesAsync(
            transactionContext,
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 12));
        Assert.Contains(new DateOnly(2026, 8, 11), transactionalDates);
        Assert.Equal(1, factory.CreatedContextCount);
    }
}
