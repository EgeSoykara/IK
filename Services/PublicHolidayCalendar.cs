using IK.Web.Database;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class PublicHolidayCalendar(
    IDbContextFactory<HumanResourcesDbContext> dbContextFactory)
{
    public async Task<HashSet<DateOnly>> GetDatesAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
        {
            return [];
        }

        await using var readContext = await dbContextFactory.CreateDbContextAsync(
            cancellationToken);
        return await QueryDatesAsync(
            readContext,
            startDate,
            endDate,
            cancellationToken);
    }

    public Task<HashSet<DateOnly>> GetDatesAsync(
        HumanResourcesDbContext transactionContext,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        return endDate < startDate
            ? Task.FromResult<HashSet<DateOnly>>([])
            : QueryDatesAsync(
                transactionContext,
                startDate,
                endDate,
                cancellationToken);
    }

    private static Task<HashSet<DateOnly>> QueryDatesAsync(
        HumanResourcesDbContext context,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken) =>
        context.PublicHolidays
            .AsNoTracking()
            .Where(holiday => holiday.Date >= startDate && holiday.Date <= endDate)
            .Select(holiday => holiday.Date)
            .ToHashSetAsync(cancellationToken);
}
