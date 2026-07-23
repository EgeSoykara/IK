using IK.Web.Models;
using IK.Web.Services;

namespace IK.Web.Tests;

public sealed class LeaveBalanceDashboardSummaryTests
{
    [Fact]
    public void SumCurrentRemainingDays_CountsLatestYearOncePerLeaveType()
    {
        var balances = new[]
        {
            CreateBalance(balanceId: 1, leaveTypeId: 1, year: 2025, remainingDays: 12),
            CreateBalance(balanceId: 2, leaveTypeId: 1, year: 2026, remainingDays: 32),
            CreateBalance(balanceId: 3, leaveTypeId: 2, year: 2026, remainingDays: 5)
        };

        var total = LeaveBalanceDashboardSummary.SumCurrentRemainingDays(balances);

        Assert.Equal(37, total);
    }

    [Theory]
    [InlineData(37, 2, 35)]
    [InlineData(37, 0.5, 36.5)]
    public void ProjectRemainingDays_SubtractsRequestedWorkingDays(
        decimal totalRemainingDays,
        decimal requestedDays,
        decimal expectedRemainingDays)
    {
        var projectedRemainingDays = LeaveBalanceDashboardSummary.ProjectRemainingDays(
            totalRemainingDays,
            requestedDays);

        Assert.Equal(expectedRemainingDays, projectedRemainingDays);
    }

    [Fact]
    public void ProjectRemainingDays_RejectsNegativeRequestedDays()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LeaveBalanceDashboardSummary.ProjectRemainingDays(37m, -0.5m));
    }

    [Fact]
    public void SelectCurrentBalances_ReturnsOneLatestBalanceForEachLeaveType()
    {
        var balances = new[]
        {
            CreateBalance(balanceId: 1, leaveTypeId: 1, year: 2024, remainingDays: 8),
            CreateBalance(balanceId: 2, leaveTypeId: 1, year: 2025, remainingDays: 18),
            CreateBalance(balanceId: 3, leaveTypeId: 2, year: 2024, remainingDays: 3),
            CreateBalance(balanceId: 4, leaveTypeId: 2, year: 2026, remainingDays: 9)
        };

        var currentBalances = LeaveBalanceDashboardSummary.SelectCurrentBalances(balances);

        Assert.Collection(
            currentBalances,
            first =>
            {
                Assert.Equal(2, first.LeaveTypeId);
                Assert.Equal(2026, first.Year);
                Assert.Equal(9, first.RemainingDays);
            },
            second =>
            {
                Assert.Equal(1, second.LeaveTypeId);
                Assert.Equal(2025, second.Year);
                Assert.Equal(18, second.RemainingDays);
            });
    }

    private static LeaveBalance CreateBalance(int balanceId, int leaveTypeId, int year, decimal remainingDays)
    {
        return new LeaveBalance
        {
            BalanceId = balanceId,
            LeaveTypeId = leaveTypeId,
            LeaveType = new LeaveType
            {
                LeaveTypeId = leaveTypeId,
                Name = $"Leave Type {leaveTypeId}"
            },
            Year = year,
            RemainingDays = remainingDays
        };
    }
}
