using IK.Web.Services;

namespace IK.Web.Tests;

public sealed class LeaveDayCalculatorTests
{
    private readonly LeaveDayCalculator calculator = new();

    [Fact]
    public void CalculateRequestedDays_WeekdayHalfDay_ReturnsHalfDay()
    {
        var weekday = new DateOnly(2026, 8, 10);

        Assert.Equal(0.5m, calculator.CalculateRequestedDays(weekday, weekday, isHalfDay: true));
    }

    [Fact]
    public void CalculateRequestedDays_WeekdayFullDay_ReturnsFullDay()
    {
        var weekday = new DateOnly(2026, 8, 10);

        Assert.Equal(1m, calculator.CalculateRequestedDays(weekday, weekday, isHalfDay: false));
    }

    [Fact]
    public void CalculateRequestedDays_RangeContainingWeekend_CountsOnlyWeekdays()
    {
        var friday = new DateOnly(2026, 8, 7);
        var monday = new DateOnly(2026, 8, 10);

        Assert.Equal(2m, calculator.CalculateRequestedDays(friday, monday, isHalfDay: false));
    }

    [Fact]
    public void CalculateRequestedDays_WeekendOnly_RejectsRequest()
    {
        var saturday = new DateOnly(2026, 8, 8);
        var sunday = new DateOnly(2026, 8, 9);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            calculator.CalculateRequestedDays(saturday, sunday, isHalfDay: false));

        Assert.Equal("Seçilen tarih aralığında iş günü bulunmuyor.", exception.Message);
    }

    [Fact]
    public void CalculateRequestedDays_MultiDayHalfDay_RejectsRequest()
    {
        var monday = new DateOnly(2026, 8, 10);
        var tuesday = new DateOnly(2026, 8, 11);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            calculator.CalculateRequestedDays(monday, tuesday, isHalfDay: true));

        Assert.Equal("Yarım gün izin yalnızca tek bir gün için seçilebilir.", exception.Message);
    }

    [Fact]
    public void CalculateRequestedDays_EndBeforeStart_RejectsRequest()
    {
        var monday = new DateOnly(2026, 8, 10);
        var tuesday = new DateOnly(2026, 8, 11);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            calculator.CalculateRequestedDays(tuesday, monday, isHalfDay: false));

        Assert.Equal("İzin bitiş tarihi, başlangıç tarihinden önce olamaz.", exception.Message);
    }

    [Fact]
    public void HaveOverlappingWorkingDays_WeekendOnlyIntersection_ReturnsFalse()
    {
        var friday = new DateOnly(2026, 8, 7);
        var saturday = new DateOnly(2026, 8, 8);
        var sunday = new DateOnly(2026, 8, 9);
        var monday = new DateOnly(2026, 8, 10);

        Assert.False(calculator.HaveOverlappingWorkingDays(friday, sunday, saturday, monday));
    }

    [Fact]
    public void HaveOverlappingWorkingDays_WeekdayIntersection_ReturnsTrue()
    {
        var friday = new DateOnly(2026, 8, 7);
        var monday = new DateOnly(2026, 8, 10);
        var tuesday = new DateOnly(2026, 8, 11);

        Assert.True(calculator.HaveOverlappingWorkingDays(friday, monday, monday, tuesday));
    }
}
