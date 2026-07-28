using IK.Web.Database;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class PublicHolidayCalendar(HumanResourcesDbContext dbContext)
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

        return await dbContext.PublicHolidays
            .AsNoTracking()
            .Where(holiday => holiday.Date >= startDate && holiday.Date <= endDate)
            .Select(holiday => holiday.Date)
            .ToHashSetAsync(cancellationToken);
    }
}
