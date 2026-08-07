namespace IK.Web.Services;

public sealed class LeaveDayCalculator
{
    public decimal CalculateRequestedDays(
        DateOnly startDate,
        DateOnly endDate,
        bool isHalfDay,
        IReadOnlySet<DateOnly> publicHolidays)
    {
        if (endDate < startDate)
        {
            throw new InvalidOperationException("İzin bitiş tarihi, başlangıç tarihinden önce olamaz.");
        }

        if (isHalfDay && startDate != endDate)
        {
            throw new InvalidOperationException("Yarım gün izin yalnızca tek bir gün için seçilebilir.");
        }

        var workingDays = CountWorkingDays(startDate, endDate, publicHolidays);

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
        DateOnly secondEndDate,
        IReadOnlySet<DateOnly> publicHolidays)
    {
        if (firstEndDate < firstStartDate || secondEndDate < secondStartDate)
        {
            throw new InvalidOperationException("İzin bitiş tarihi, başlangıç tarihinden önce olamaz.");
        }

        var overlapStart = firstStartDate > secondStartDate ? firstStartDate : secondStartDate;
        var overlapEnd = firstEndDate < secondEndDate ? firstEndDate : secondEndDate;

        return overlapStart <= overlapEnd
            && CountWorkingDays(overlapStart, overlapEnd, publicHolidays) > 0;
    }

    public int CountWorkingDays(
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlySet<DateOnly> publicHolidays) =>
        GetWorkingDates(startDate, endDate, publicHolidays).Count;

    public IReadOnlyList<DateOnly> GetWorkingDates(
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlySet<DateOnly> publicHolidays)
    {
        if (endDate < startDate)
        {
            throw new InvalidOperationException("İzin bitiş tarihi, başlangıç tarihinden önce olamaz.");
        }

        var workingDates = new List<DateOnly>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (IsWorkingDay(date, publicHolidays))
            {
                workingDates.Add(date);
            }
        }

        return workingDates;
    }

    public static bool IsWorkingDay(DateOnly date, IReadOnlySet<DateOnly> publicHolidays) =>
        date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday
        && !publicHolidays.Contains(date);
}
