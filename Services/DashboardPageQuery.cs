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

    public static async Task<List<DashboardPendingItem>> LoadPendingItemsAsync(
        HumanResourcesDbContext database,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var pendingLeaves = await database.LeaveRequests
            .AsNoTracking()
            .Where(request => request.EmployeeId == employeeId
                && (request.CurrentStatus == LeaveRequestStatus.ManagerReview
                    || request.CurrentStatus == LeaveRequestStatus.HumanResourcesReview))
            .OrderBy(request => request.StartDate)
            .ThenBy(request => request.RequestId)
            .Take(PendingRequestPreviewLimit)
            .Select(request => new DashboardPendingItem(
                request.RequestId,
                false,
                request.Category,
                request.LeaveType == null ? null : request.LeaveType.Name,
                request.StartDate,
                request.EndDate,
                request.RequestedDays,
                request.CurrentStatus))
            .ToListAsync(cancellationToken);
        var pendingCancellations = await database.LeaveCancellationRequests
            .AsNoTracking()
            .Where(request => request.LeaveRequest.EmployeeId == employeeId
                && (request.CurrentStatus == LeaveRequestStatus.ManagerReview
                    || request.CurrentStatus == LeaveRequestStatus.HumanResourcesReview))
            .OrderBy(request => request.CancellationStartDate)
            .ThenBy(request => request.CancellationRequestId)
            .Take(PendingRequestPreviewLimit)
            .Select(request => new DashboardPendingItem(
                request.CancellationRequestId,
                true,
                request.LeaveRequest.Category,
                request.LeaveRequest.LeaveType == null ? null : request.LeaveRequest.LeaveType.Name,
                request.CancellationStartDate,
                request.CancellationEndDate,
                request.RequestedRefundDays,
                request.CurrentStatus))
            .ToListAsync(cancellationToken);

        return pendingLeaves
            .Concat(pendingCancellations)
            .OrderBy(request => request.StartDate)
            .ThenBy(request => request.Id)
            .Take(PendingRequestPreviewLimit)
            .ToList();
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
                    || request.CurrentStatus == LeaveRequestStatus.Rejected
                    || request.CurrentStatus == LeaveRequestStatus.Cancelled),
                group.Count(request => request.CurrentStatus == LeaveRequestStatus.Approved),
                group.Count(request => request.CurrentStatus == LeaveRequestStatus.Rejected),
                group.Count(request => request.CurrentStatus == LeaveRequestStatus.Cancelled),
                group
                    .Where(request =>
                        request.CurrentStatus == LeaveRequestStatus.Approved
                        || request.CurrentStatus == LeaveRequestStatus.Rejected
                        || request.CurrentStatus == LeaveRequestStatus.Cancelled)
                    .Max(request => (DateTimeOffset?)request.UpdatedAt)))
            .SingleOrDefaultAsync(cancellationToken);

        var pendingCancellationCount = await database.LeaveCancellationRequests
            .AsNoTracking()
            .CountAsync(request => request.LeaveRequest.EmployeeId == employeeId
                && (request.CurrentStatus == LeaveRequestStatus.ManagerReview
                    || request.CurrentStatus == LeaveRequestStatus.HumanResourcesReview),
                cancellationToken);
        var result = summary ?? DashboardRequestSummary.Empty;
        return result with { PendingCount = result.PendingCount + pendingCancellationCount };
    }
}

public sealed record DashboardPendingItem(
    int Id,
    bool IsCancellation,
    LeaveRequestCategory Category,
    string? LeaveTypeName,
    DateTime? StartDate,
    DateTime? EndDate,
    decimal Days,
    LeaveRequestStatus Status)
{
    public string DisplayName =>
        $"{(IsCancellation ? "İzin İptali · " : string.Empty)}{(Category == LeaveRequestCategory.AnnualLeave ? "Yıllık İzin" : LeaveTypeName ?? "İzin")}";
}

public sealed record DashboardRequestSummary(
    int PendingCount,
    int CompletedCount,
    int ApprovedCount,
    int RejectedCount,
    int CancelledCount,
    DateTimeOffset? LatestCompletedRequestDate)
{
    public static DashboardRequestSummary Empty { get; } = new(0, 0, 0, 0, 0, null);
}
