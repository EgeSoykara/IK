using IK.Web.Services;

namespace IK.Web.Tests;

public sealed class LeaveDayCalculatorTests
{
    private readonly LeaveDayCalculator calculator = new();
    private static readonly IReadOnlySet<DateOnly> NoPublicHolidays = new HashSet<DateOnly>();

    [Fact]
    public void CalculateRequestedDays_WeekdayHalfDay_ReturnsHalfDay()
    {
        var weekday = new DateOnly(2026, 8, 10);

        Assert.Equal(0.5m, calculator.CalculateRequestedDays(weekday, weekday, isHalfDay: true, NoPublicHolidays));
    }

    [Fact]
    public void CalculateRequestedDays_WeekdayFullDay_ReturnsFullDay()
    {
        var weekday = new DateOnly(2026, 8, 10);

        Assert.Equal(1m, calculator.CalculateRequestedDays(weekday, weekday, isHalfDay: false, NoPublicHolidays));
    }

    [Fact]
    public void CalculateRequestedDays_RangeContainingWeekend_CountsOnlyWeekdays()
    {
        var friday = new DateOnly(2026, 8, 7);
        var monday = new DateOnly(2026, 8, 10);

        Assert.Equal(2m, calculator.CalculateRequestedDays(friday, monday, isHalfDay: false, NoPublicHolidays));
    }

    [Fact]
    public void CalculateRequestedDays_WeekendOnly_RejectsRequest()
    {
        var saturday = new DateOnly(2026, 8, 8);
        var sunday = new DateOnly(2026, 8, 9);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            calculator.CalculateRequestedDays(saturday, sunday, isHalfDay: false, NoPublicHolidays));

        Assert.Equal("Seçilen tarih aralığında iş günü bulunmuyor.", exception.Message);
    }

    [Fact]
    public void CalculateRequestedDays_MultiDayHalfDay_RejectsRequest()
    {
        var monday = new DateOnly(2026, 8, 10);
        var tuesday = new DateOnly(2026, 8, 11);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            calculator.CalculateRequestedDays(monday, tuesday, isHalfDay: true, NoPublicHolidays));

        Assert.Equal("Yarım gün izin yalnızca tek bir gün için seçilebilir.", exception.Message);
    }

    [Fact]
    public void CalculateRequestedDays_EndBeforeStart_RejectsRequest()
    {
        var monday = new DateOnly(2026, 8, 10);
        var tuesday = new DateOnly(2026, 8, 11);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            calculator.CalculateRequestedDays(tuesday, monday, isHalfDay: false, NoPublicHolidays));

        Assert.Equal("İzin bitiş tarihi, başlangıç tarihinden önce olamaz.", exception.Message);
    }

    [Fact]
    public void HaveOverlappingWorkingDays_WeekendOnlyIntersection_ReturnsFalse()
    {
        var friday = new DateOnly(2026, 8, 7);
        var saturday = new DateOnly(2026, 8, 8);
        var sunday = new DateOnly(2026, 8, 9);
        var monday = new DateOnly(2026, 8, 10);

        Assert.False(calculator.HaveOverlappingWorkingDays(friday, sunday, saturday, monday, NoPublicHolidays));
    }

    [Fact]
    public void HaveOverlappingWorkingDays_WeekdayIntersection_ReturnsTrue()
    {
        var friday = new DateOnly(2026, 8, 7);
        var monday = new DateOnly(2026, 8, 10);
        var tuesday = new DateOnly(2026, 8, 11);

        Assert.True(calculator.HaveOverlappingWorkingDays(friday, monday, monday, tuesday, NoPublicHolidays));
    }

    [Fact]
    public void CalculateRequestedDays_WeekdayPublicHoliday_ExcludesHoliday()
    {
        var monday = new DateOnly(2026, 8, 10);
        var tuesday = new DateOnly(2026, 8, 11);
        IReadOnlySet<DateOnly> holidays = new HashSet<DateOnly> { monday };

        Assert.Equal(1m, calculator.CalculateRequestedDays(monday, tuesday, false, holidays));
    }

    [Fact]
    public void CalculateRequestedDays_PublicHolidayOnly_RejectsRequest()
    {
        var monday = new DateOnly(2026, 8, 10);
        IReadOnlySet<DateOnly> holidays = new HashSet<DateOnly> { monday };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            calculator.CalculateRequestedDays(monday, monday, false, holidays));

        Assert.Equal("Seçilen tarih aralığında iş günü bulunmuyor.", exception.Message);
    }

    [Fact]
    public void HaveOverlappingWorkingDays_PublicHolidayOnlyIntersection_ReturnsFalse()
    {
        var monday = new DateOnly(2026, 8, 10);
        var tuesday = new DateOnly(2026, 8, 11);
        IReadOnlySet<DateOnly> holidays = new HashSet<DateOnly> { monday };

        Assert.False(calculator.HaveOverlappingWorkingDays(
            monday,
            monday,
            monday,
            tuesday,
            holidays));
    }
}
