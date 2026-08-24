using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class LeaveTrackingService(
    HumanResourcesDbContext dbContext,
    PageAccessService pageAccessService)
{
    public async Task<int?> ResolveInitialDepartmentIdAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!pageAccessService.CanAccessAuthenticatedPages(principal))
        {
            throw new UnauthorizedAccessException("Oturum açmanız gerekir.");
        }

        var employeeId = principal.GetEmployeeId()
            ?? throw new UnauthorizedAccessException(
                "Kullanıcıya bağlı çalışan kaydı bulunamadı.");
        var ownDepartmentId = await dbContext.Employees
            .AsNoTracking()
            .Where(employee => employee.EmployeeId == employeeId)
            .Select(employee => (int?)employee.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Çalışanın departmanı bulunamadı.");
        if (!pageAccessService.CanViewLeaveRequests(principal))
        {
            return ownDepartmentId;
        }

        return await dbContext.Departments
            .AsNoTracking()
            .Where(department =>
                department.ManagerEmployeeId == employeeId
                || department.ActiveDelegateEmployeeId == employeeId)
            .OrderByDescending(department =>
                department.ManagerEmployeeId == employeeId)
            .ThenByDescending(department =>
                department.DepartmentId == ownDepartmentId)
            .ThenBy(department => department.DepartmentName)
            .ThenBy(department => department.DepartmentId)
            .Select(department => (int?)department.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);
    }

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
        var holidayRows = await dbContext.PublicHolidays
            .AsNoTracking()
            .Where(holiday => holiday.Date >= holidayRangeStart && holiday.Date <= holidayRangeEnd)
            .OrderBy(holiday => holiday.Date)
            .Select(holiday => new LeaveTrackingPublicHoliday(holiday.Date, holiday.Name))
            .ToListAsync(cancellationToken);
        var publicHolidays = holidayRows
            .Select(holiday => holiday.Date)
            .ToHashSet();
        var visiblePublicHolidays = holidayRows
            .Where(holiday => holiday.Date >= monthStartDate && holiday.Date <= monthEndDate)
            .ToList();
        var requestsQuery = dbContext.LeaveRequests
            .AsNoTracking()
            .Include(item => item.Employee)
            .ThenInclude(employee => employee.Department)
            .Include(item => item.ManagerApprover)
            .Include(item => item.LeaveType)
            .Include(item => item.ApprovedDays)
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

        var creatorEmployeeIds = creatorLogs
            .Where(item => item.ActorEmployeeId.HasValue)
            .Select(item => item.ActorEmployeeId!.Value)
            .Distinct()
            .ToArray();
        var creatorNames = await dbContext.Employees
            .AsNoTracking()
            .Where(item => creatorEmployeeIds.Contains(item.EmployeeId))
            .ToDictionaryAsync(
                item => item.EmployeeId,
                item => item.FirstName + " " + item.LastName,
                cancellationToken);
        var creators = creatorLogs
            .Where(item => int.TryParse(item.EntityId, out _))
            .GroupBy(item => int.Parse(item.EntityId))
            .ToDictionary(
                group => group.Key,
                group => FormatCreator(
                    group.OrderByDescending(item => item.ActionDate).First(),
                    creatorNames));

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
                item.Employee.Department != null ? item.Employee.Department.DepartmentName : "Atanmadı",
                item.DisplayName(),
                DateOnly.FromDateTime(item.StartDate!.Value),
                DateOnly.FromDateTime(item.EndDate!.Value),
                WorkingDates(
                    DateOnly.FromDateTime(item.StartDate.Value),
                    DateOnly.FromDateTime(item.EndDate.Value),
                    monthStartDate,
                    monthEndDate,
                    publicHolidays,
                    item.CurrentStatus,
                    item.ApprovedDays),
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
            .OrderBy(item => item.Department != null ? item.Department.DepartmentName : string.Empty)
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
                               && (item.CurrentStatus != LeaveRequestStatus.Approved
                                   || item.ApprovedDays.Any(day => day.WorkDate == rosterDateTime))
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
                employee.Department != null ? employee.Department.DepartmentName : "Atanmadı",
                status);
        }).ToList();

        var departments = canViewAll
            ? await dbContext.Departments
                .AsNoTracking()
                .OrderBy(item => item.DepartmentName)
                .Select(item => new LeaveTrackingDepartment(item.DepartmentId, item.DepartmentName))
                .ToListAsync(cancellationToken)
            : [];

        return new LeaveTrackingSnapshot(
            canViewAll,
            departmentId,
            departments,
            visiblePublicHolidays,
            events,
            roster);
    }

    private static string FormatCreator(
        AuditLog auditLog,
        IReadOnlyDictionary<int, string> employeeNames)
    {
        if (auditLog.ActorEmployeeId is { } employeeId)
        {
            return employeeNames.TryGetValue(employeeId, out var employeeName)
                ? employeeName
                : $"Silinmiş çalışan (#{employeeId})";
        }

        return auditLog.SystemActorKey == DailyLeaveEntitlementWorker.SystemActor
            ? "Günlük İzin Otomasyonu"
            : "Sistem";
    }

    private static IReadOnlyList<DateOnly> WorkingDates(
        DateOnly requestStart,
        DateOnly requestEnd,
        DateOnly monthStart,
        DateOnly monthEnd,
        IReadOnlySet<DateOnly> publicHolidays,
        LeaveRequestStatus requestStatus,
        IEnumerable<LeaveRequestApprovedDay> approvedDays)
    {
        var start = requestStart > monthStart ? requestStart : monthStart;
        var end = requestEnd < monthEnd ? requestEnd : monthEnd;
        if (requestStatus == LeaveRequestStatus.Approved)
        {
            return approvedDays
                .Select(item => DateOnly.FromDateTime(item.WorkDate))
                .Where(date => date >= start && date <= end)
                .OrderBy(date => date)
                .ToList();
        }

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
    IReadOnlyList<LeaveTrackingPublicHoliday> PublicHolidays,
    IReadOnlyList<LeaveTrackingEvent> Events,
    IReadOnlyList<WorkforceLeaveRow> Roster);

public sealed record LeaveTrackingDepartment(int DepartmentId, string DepartmentName);

public sealed record LeaveTrackingPublicHoliday(DateOnly Date, string Name);

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
