using IK.Web.Models;

namespace IK.Web.Services;

internal static class LeaveBalanceDashboardSummary
{
    public static IReadOnlyList<LeaveBalance> SelectCurrentBalances(IEnumerable<LeaveBalance> balances)
    {
        ArgumentNullException.ThrowIfNull(balances);

        return balances
            .GroupBy(balance => balance.LeaveTypeId)
            .Select(group => group
                .OrderByDescending(balance => balance.Year)
                .ThenByDescending(balance => balance.BalanceId)
                .First())
            .OrderByDescending(balance => balance.Year)
            .ThenBy(balance => balance.LeaveType?.Name ?? string.Empty)
            .ThenBy(balance => balance.LeaveTypeId)
            .ToList();
    }

    public static decimal SumCurrentRemainingDays(IEnumerable<LeaveBalance> balances)
    {
        return SelectCurrentBalances(balances).Sum(balance => balance.RemainingDays);
    }

    public static decimal ProjectRemainingDays(decimal totalRemainingDays, decimal requestedDays)
    {
        if (requestedDays < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedDays));
        }

        return totalRemainingDays - requestedDays;
    }
}
