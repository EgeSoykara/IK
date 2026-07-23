namespace IK.Web.Services;

public sealed class LeaveDayCalculator
{
    public decimal CalculateRequestedDays(DateOnly startDate, DateOnly endDate, bool isHalfDay)
    {
        if (endDate < startDate)
        {
            throw new InvalidOperationException("İzin bitiş tarihi, başlangıç tarihinden önce olamaz.");
        }

        if (isHalfDay && startDate != endDate)
        {
            throw new InvalidOperationException("Yarım gün izin yalnızca tek bir gün için seçilebilir.");
        }

        var workingDays = CountWorkingDays(startDate, endDate);

        if (workingDays == 0)
        {
            throw new InvalidOperationException("Seçilen tarih aralığında iş günü bulunmuyor.");
        }

        return isHalfDay ? 0.5m : workingDays;
    }

    public bool HaveOverlappingWorkingDays(
        DateOnly firstStartDate,
        DateOnly firstEndDate,
        DateOnly secondStartDate,
        DateOnly secondEndDate)
    {
        if (firstEndDate < firstStartDate || secondEndDate < secondStartDate)
        {
            throw new InvalidOperationException("İzin bitiş tarihi, başlangıç tarihinden önce olamaz.");
        }

        var overlapStart = firstStartDate > secondStartDate ? firstStartDate : secondStartDate;
        var overlapEnd = firstEndDate < secondEndDate ? firstEndDate : secondEndDate;

        return overlapStart <= overlapEnd && CountWorkingDays(overlapStart, overlapEnd) > 0;
    }

    private static int CountWorkingDays(DateOnly startDate, DateOnly endDate)
    {
        var totalDays = endDate.DayNumber - startDate.DayNumber + 1;
        var workingDays = totalDays / 7 * 5;
        var remainingDays = totalDays % 7;

        for (var dayOffset = 0; dayOffset < remainingDays; dayOffset++)
        {
            if (IsWorkingDay(startDate.AddDays(dayOffset)))
            {
                workingDays++;
            }
        }

        return workingDays;
    }

    private static bool IsWorkingDay(DateOnly date) =>
        date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
}
