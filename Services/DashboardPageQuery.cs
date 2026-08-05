using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public static class DashboardPageQuery
{
    public const int PendingRequestPreviewLimit = 3;

    public static async Task<List<LeaveBalance>> LoadCurrentBalancesAsync(
        HumanResourcesDbContext database,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var latestBalanceYears = database.LeaveBalances
            .AsNoTracking()
            .Where(balance => balance.EmployeeId == employeeId)
            .GroupBy(balance => balance.LeaveTypeId)
            .Select(group => new
            {
                LeaveTypeId = group.Key,
                Year = group.Max(balance => balance.Year)
            });

        return await database.LeaveBalances
            .AsNoTracking()
            .Include(balance => balance.LeaveType)
            .Where(balance => balance.EmployeeId == employeeId
                && latestBalanceYears.Any(latest =>
                    latest.LeaveTypeId == balance.LeaveTypeId
                    && latest.Year == balance.Year))
            .OrderBy(balance => balance.LeaveType.Name)
            .ToListAsync(cancellationToken);
    }

    public static Task<List<LeaveRequest>> LoadPendingRequestPreviewsAsync(
        HumanResourcesDbContext database,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return database.LeaveRequests
            .AsNoTracking()
            .Include(request => request.LeaveType)
            .Where(request => request.EmployeeId == employeeId
                && (request.CurrentStatus == LeaveRequestStatus.ManagerReview
                    || request.CurrentStatus == LeaveRequestStatus.HumanResourcesReview))
            .OrderBy(request => request.StartDate)
            .ThenBy(request => request.RequestId)
            .Take(PendingRequestPreviewLimit)
            .ToListAsync(cancellationToken);
    }

    public static async Task<DashboardRequestSummary> LoadRequestSummaryAsync(
        HumanResourcesDbContext database,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var summary = await database.LeaveRequests
            .AsNoTracking()
            .Where(request => request.EmployeeId == employeeId)
            .GroupBy(_ => 1)
            .Select(group => new DashboardRequestSummary(
                group.Count(request =>
                    request.CurrentStatus == LeaveRequestStatus.ManagerReview
                    || request.CurrentStatus == LeaveRequestStatus.HumanResourcesReview),
                group.Count(request =>
                    request.CurrentStatus == LeaveRequestStatus.Approved
                    || request.CurrentStatus == LeaveRequestStatus.Rejected),
                group.Count(request => request.CurrentStatus == LeaveRequestStatus.Approved),
                group.Count(request => request.CurrentStatus == LeaveRequestStatus.Rejected),
                group
                    .Where(request =>
                        request.CurrentStatus == LeaveRequestStatus.Approved
                        || request.CurrentStatus == LeaveRequestStatus.Rejected)
                    .Max(request => (DateTimeOffset?)request.UpdatedAt)))
            .SingleOrDefaultAsync(cancellationToken);

        return summary ?? DashboardRequestSummary.Empty;
    }
}

public sealed record DashboardRequestSummary(
    int PendingCount,
    int CompletedCount,
    int ApprovedCount,
    int RejectedCount,
    DateTimeOffset? LatestCompletedRequestDate)
{
    public static DashboardRequestSummary Empty { get; } = new(0, 0, 0, 0, null);
}
