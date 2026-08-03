using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class LeaveTrackingService(
    HumanResourcesDbContext dbContext,
    PageAccessService pageAccessService,
    PublicHolidayCalendar publicHolidayCalendar)
{
    public async Task<LeaveTrackingSnapshot> GetSnapshotAsync(
        ClaimsPrincipal principal,
        DateOnly month,
        int? requestedDepartmentId,
        DateOnly rosterDate,
        CancellationToken cancellationToken = default)
    {
        if (!pageAccessService.CanAccessAuthenticatedPages(principal))
        {
            throw new UnauthorizedAccessException("Oturum açmanız gerekir.");
        }

        var canViewAll = pageAccessService.CanViewLeaveRequests(principal);
        var employeeId = principal.GetEmployeeId();
        if (!employeeId.HasValue)
        {
            throw new UnauthorizedAccessException("Kullanıcıya bağlı çalışan kaydı bulunamadı.");
        }

        var ownDepartmentId = await dbContext.Employees
            .AsNoTracking()
            .Where(item => item.EmployeeId == employeeId.Value)
            .Select(item => (int?)item.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Çalışanın departmanı bulunamadı.");

        int? departmentId;
        if (canViewAll)
        {
            departmentId = requestedDepartmentId;
            if (departmentId.HasValue
                && !await dbContext.Departments
                    .AsNoTracking()
                    .AnyAsync(item => item.DepartmentId == departmentId.Value, cancellationToken))
            {
                throw new InvalidOperationException("Seçilen departman bulunamadı.");
            }
        }
        else
        {
            if (requestedDepartmentId.HasValue && requestedDepartmentId != ownDepartmentId)
            {
                throw new UnauthorizedAccessException(
                    "Yalnız kendi departmanınızın izin takibini görüntüleyebilirsiniz.");
            }

            departmentId = ownDepartmentId;
        }

        var monthStartDate = new DateOnly(month.Year, month.Month, 1);
        var monthEndDate = monthStartDate.AddMonths(1).AddDays(-1);
        var monthStart = monthStartDate.ToDateTime(TimeOnly.MinValue);
        var monthEnd = monthEndDate.ToDateTime(TimeOnly.MinValue);
        var holidayRangeStart = rosterDate < monthStartDate ? rosterDate : monthStartDate;
        var holidayRangeEnd = rosterDate > monthEndDate ? rosterDate : monthEndDate;
        var publicHolidays = await publicHolidayCalendar.GetDatesAsync(
            holidayRangeStart,
            holidayRangeEnd,
            cancellationToken);
        var requestsQuery = dbContext.LeaveRequests
            .AsNoTracking()
            .Include(item => item.Employee)
            .ThenInclude(employee => employee.Department)
            .Include(item => item.ManagerApprover)
            .Include(item => item.LeaveType)
            .Where(item => item.StartDate != null
                           && item.EndDate != null
                           && item.StartDate.Value.Date <= monthEnd
                           && item.EndDate.Value.Date >= monthStart
                           && (item.CurrentStatus == LeaveRequestStatus.ManagerReview
                               || item.CurrentStatus == LeaveRequestStatus.HumanResourcesReview
                               || item.CurrentStatus == LeaveRequestStatus.Approved));
        if (departmentId.HasValue)
        {
            requestsQuery = requestsQuery.Where(item => item.Employee.DepartmentId == departmentId.Value);
        }

        var requests = await requestsQuery
            .OrderBy(item => item.StartDate)
            .ThenBy(item => item.Employee.FirstName)
            .ThenBy(item => item.Employee.LastName)
            .ToListAsync(cancellationToken);
        var requestIds = requests.Select(item => item.RequestId).ToList();
        var creatorLogs = new List<AuditLog>();
        foreach (var requestIdBatch in requestIds.Chunk(500))
        {
            var requestIdStrings = requestIdBatch.Select(item => item.ToString()).ToList();
            creatorLogs.AddRange(await dbContext.AuditLogs
                .AsNoTracking()
                .Where(item => item.ActionType == AuditActionType.LeaveRequestCreated
                               && item.EntityName == nameof(LeaveRequest)
                               && requestIdStrings.Contains(item.EntityId))
                .ToListAsync(cancellationToken));
        }

        var creators = creatorLogs
            .Where(item => int.TryParse(item.EntityId, out _))
            .GroupBy(item => int.Parse(item.EntityId))
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.ActionDate).First().UserId);

        var approvals = new List<LeaveApproval>();
        foreach (var requestIdBatch in requestIds.Chunk(500))
        {
            approvals.AddRange(await dbContext.LeaveApprovals
                .AsNoTracking()
                .Include(item => item.ApproverEmployee)
                .Where(item => requestIdBatch.Contains(item.RequestId)
                               && item.Decision == LeaveApprovalDecision.Approved)
                .ToListAsync(cancellationToken));
        }

        var approvers = approvals
            .GroupBy(item => item.RequestId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var approval = group
                        .OrderByDescending(item => item.ApproverRole)
                        .ThenByDescending(item => item.DecisionDate)
                        .First();
                    return approval.ApproverEmployee is null
                        ? "Sistem"
                        : $"{approval.ApproverEmployee.FirstName} {approval.ApproverEmployee.LastName}";
                });

        var events = requests.Select(item => new LeaveTrackingEvent(
                item.RequestId,
                item.EmployeeId,
                $"{item.Employee.FirstName} {item.Employee.LastName}",
                item.Employee.Department.DepartmentName,
                item.DisplayName(),
                DateOnly.FromDateTime(item.StartDate!.Value),
                DateOnly.FromDateTime(item.EndDate!.Value),
                WorkingDates(
                    DateOnly.FromDateTime(item.StartDate.Value),
                    DateOnly.FromDateTime(item.EndDate.Value),
                    monthStartDate,
                    monthEndDate,
                    publicHolidays),
                item.CurrentStatus,
                creators.GetValueOrDefault(item.RequestId, "Bilinmiyor"),
                item.CurrentStatus == LeaveRequestStatus.Approved
                    ? approvers.GetValueOrDefault(item.RequestId, "Bilinmiyor")
                    : null))
            .ToList();

        var employeesQuery = dbContext.Employees
            .AsNoTracking()
            .Include(item => item.Department)
            .Where(item => item.Status == EmploymentStatus.Active);
        if (departmentId.HasValue)
        {
            employeesQuery = employeesQuery.Where(item => item.DepartmentId == departmentId.Value);
        }

        var employees = await employeesQuery
            .OrderBy(item => item.Department.DepartmentName)
            .ThenBy(item => item.FirstName)
            .ThenBy(item => item.LastName)
            .ToListAsync(cancellationToken);
        var rosterDateTime = rosterDate.ToDateTime(TimeOnly.MinValue);
        var rosterRequests = new List<LeaveRequest>();
        var isRosterWorkingDay = LeaveDayCalculator.IsWorkingDay(rosterDate, publicHolidays);
        if (isRosterWorkingDay)
        {
            var rosterRequestsQuery = dbContext.LeaveRequests
                .AsNoTracking()
                .Where(item => item.Employee.Status == EmploymentStatus.Active
                               && item.StartDate != null
                               && item.EndDate != null
                               && item.StartDate.Value.Date <= rosterDateTime
                               && item.EndDate.Value.Date >= rosterDateTime
                               && (item.CurrentStatus == LeaveRequestStatus.ManagerReview
                                   || item.CurrentStatus == LeaveRequestStatus.HumanResourcesReview
                                   || item.CurrentStatus == LeaveRequestStatus.Approved));
            if (departmentId.HasValue)
            {
                rosterRequestsQuery = rosterRequestsQuery
                    .Where(item => item.Employee.DepartmentId == departmentId.Value);
            }

            rosterRequests = await rosterRequestsQuery.ToListAsync(cancellationToken);
        }

        var rosterByEmployee = rosterRequests
            .GroupBy(item => item.EmployeeId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var roster = employees.Select(employee =>
        {
            var employeeRequests = rosterByEmployee.GetValueOrDefault(employee.EmployeeId) ?? [];
            var status = !isRosterWorkingDay
                ? WorkforceLeaveStatus.NonWorkingDay
                : employeeRequests.Any(item => item.CurrentStatus == LeaveRequestStatus.Approved)
                    ? WorkforceLeaveStatus.OnLeave
                    : employeeRequests.Count > 0
                        ? WorkforceLeaveStatus.Pending
                        : WorkforceLeaveStatus.Working;
            return new WorkforceLeaveRow(
                employee.EmployeeId,
                $"{employee.FirstName} {employee.LastName}",
                employee.Department.DepartmentName,
                status);
        }).ToList();

        var departments = canViewAll
            ? await dbContext.Departments
                .AsNoTracking()
                .OrderBy(item => item.DepartmentName)
                .Select(item => new LeaveTrackingDepartment(item.DepartmentId, item.DepartmentName))
                .ToListAsync(cancellationToken)
            : [];

        return new LeaveTrackingSnapshot(canViewAll, departmentId, departments, events, roster);
    }

    private static IReadOnlyList<DateOnly> WorkingDates(
        DateOnly requestStart,
        DateOnly requestEnd,
        DateOnly monthStart,
        DateOnly monthEnd,
        IReadOnlySet<DateOnly> publicHolidays)
    {
        var start = requestStart > monthStart ? requestStart : monthStart;
        var end = requestEnd < monthEnd ? requestEnd : monthEnd;
        var dates = new List<DateOnly>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (LeaveDayCalculator.IsWorkingDay(date, publicHolidays))
            {
                dates.Add(date);
            }
        }

        return dates;
    }
}

public sealed record LeaveTrackingSnapshot(
    bool CanViewAllDepartments,
    int? DepartmentId,
    IReadOnlyList<LeaveTrackingDepartment> Departments,
    IReadOnlyList<LeaveTrackingEvent> Events,
    IReadOnlyList<WorkforceLeaveRow> Roster);

public sealed record LeaveTrackingDepartment(int DepartmentId, string DepartmentName);

public sealed record LeaveTrackingEvent(
    int RequestId,
    int EmployeeId,
    string EmployeeName,
    string DepartmentName,
    string CategoryName,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<DateOnly> WorkingDates,
    LeaveRequestStatus Status,
    string RequestedBy,
    string? ApprovedBy);

public sealed record WorkforceLeaveRow(
    int EmployeeId,
    string EmployeeName,
    string DepartmentName,
    WorkforceLeaveStatus Status);

public enum WorkforceLeaveStatus
{
    Working,
    Pending,
    OnLeave,
    NonWorkingDay
}
