using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class LeaveRequestService(
    HumanResourcesDbContext dbContext,
    LeaveDayCalculator dayCalculator,
    AuditLogService auditLogService)
{
    private static readonly LeaveRequestStatus[] BalanceBlockingStatuses =
    [
        LeaveRequestStatus.ManagerReview,
        LeaveRequestStatus.HumanResourcesReview,
        LeaveRequestStatus.Approved
    ];

    public async Task<LeaveRequest> CreateRequestAsync(
        int employeeId,
        int leaveTypeId,
        DateTime? startDate,
        DateTime? endDate,
        string reason,
        string actorUserId,
        bool isHalfDay = false,
        CancellationToken cancellationToken = default)
    {
        if (startDate is null || endDate is null)
        {
            throw new InvalidOperationException("İzin talebinin başlangıç ve bitiş tarihleri zorunludur.");
        }

        var requestStartDate = startDate.Value.Date;
        var requestEndDate = endDate.Value.Date;
        ValidateRequestPeriod(requestStartDate, requestEndDate);
        var requestStartDay = requestStartDate.Date;
        var requestEndDay = requestEndDate.Date;
        var requestedDays = dayCalculator.CalculateRequestedDays(
            DateOnly.FromDateTime(requestStartDate),
            DateOnly.FromDateTime(requestEndDate),
            isHalfDay);
        var trimmedReason = RequireText(reason, "İzin talep nedeni zorunludur.");

        if (requestStartDay.Year != requestEndDay.Year)
        {
            throw new InvalidOperationException("1. aşamada izin talepleri yıl sınırını aşamaz.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(item => item.EmployeeId == employeeId, cancellationToken);

        employee = ValidateEmployeeForRequest(employee);

        var balance = await dbContext.LeaveBalances
            .SingleOrDefaultAsync(
                item => item.EmployeeId == employeeId &&
                        item.LeaveTypeId == leaveTypeId &&
                        item.Year == requestStartDay.Year,
                cancellationToken);

        if (balance is null)
        {
            throw new InvalidOperationException("Talep yılı için izin bakiyesi bulunamadı.");
        }

        ValidateBalanceForRequest(balance, requestedDays);

        var overlapsExistingRequest = await HasOverlappingRequestAsync(
            employeeId,
            requestStartDate,
            requestEndDate,
            excludedRequestId: null,
            cancellationToken);

        if (overlapsExistingRequest)
        {
            throw new InvalidOperationException("Çalışanın bu dönemle çakışan başka bir izin talebi zaten mevcut.");
        }

        var now = DateTimeOffset.UtcNow;
        var leaveRequest = new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveTypeId = leaveTypeId,
            StartDate = requestStartDate,
            EndDate = requestEndDate,
            RequestedDays = requestedDays,
            Reason = trimmedReason,
            CurrentStatus = LeaveRequestStatus.ManagerReview,
            ManagerApproverEmployeeId = employee.ManagerId,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.LeaveRequests.Add(leaveRequest);
        dbContext.LeaveApprovals.Add(new LeaveApproval
        {
            Request = leaveRequest,
            ApproverRole = LeaveApproverRole.Manager,
            ApproverEmployeeId = employee.ManagerId,
            Decision = LeaveApprovalDecision.Pending,
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.AppendAsync(
            AuditActionType.LeaveRequestCreated,
            nameof(LeaveRequest),
            leaveRequest.RequestId.ToString(),
            actorUserId,
            $"EmployeeId={employeeId}; LeaveTypeId={leaveTypeId}; RequestedDays={requestedDays:0.##}",
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return leaveRequest;
    }

    public async Task<LeaveRequest> UpdateRequestAsync(
        int requestId,
        int employeeId,
        int leaveTypeId,
        DateTime? startDate,
        DateTime? endDate,
        string reason,
        string actorUserId,
        bool isHalfDay = false,
        CancellationToken cancellationToken = default)
    {
        if (startDate is null || endDate is null)
        {
            throw new InvalidOperationException("İzin talebinin başlangıç ve bitiş tarihleri zorunludur.");
        }

        var requestStartDate = startDate.Value.Date;
        var requestEndDate = endDate.Value.Date;
        ValidateRequestPeriod(requestStartDate, requestEndDate);
        var requestStartDay = requestStartDate.Date;
        var requestEndDay = requestEndDate.Date;
        var requestedDays = dayCalculator.CalculateRequestedDays(
            DateOnly.FromDateTime(requestStartDate),
            DateOnly.FromDateTime(requestEndDate),
            isHalfDay);
        var trimmedReason = RequireText(reason, "İzin talep nedeni zorunludur.");

        if (requestStartDay.Year != requestEndDay.Year)
        {
            throw new InvalidOperationException("1. aşamada izin talepleri yıl sınırını aşamaz.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

        var request = await dbContext.LeaveRequests
            .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);

        if (request is null)
        {
            throw new InvalidOperationException("İzin talebi bulunamadı.");
        }

        if (request.CurrentStatus != LeaveRequestStatus.ManagerReview)
        {
            throw new InvalidOperationException("Yalnızca yönetici onayı bekleyen izin talepleri düzenlenebilir.");
        }

        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(item => item.EmployeeId == employeeId, cancellationToken);

        employee = ValidateEmployeeForRequest(employee);

        var leaveTypeExists = await dbContext.LeaveTypes
            .AnyAsync(item => item.LeaveTypeId == leaveTypeId, cancellationToken);

        if (!leaveTypeExists)
        {
            throw new InvalidOperationException("İzin türü bulunamadı.");
        }

        var balance = await dbContext.LeaveBalances
            .SingleOrDefaultAsync(
                item => item.EmployeeId == employeeId &&
                        item.LeaveTypeId == leaveTypeId &&
                        item.Year == requestStartDay.Year,
                cancellationToken);

        if (balance is null)
        {
            throw new InvalidOperationException("Talep yılı için izin bakiyesi bulunamadı.");
        }

        ValidateBalanceForRequest(balance, requestedDays);

        var overlapsExistingRequest = await HasOverlappingRequestAsync(
            employeeId,
            requestStartDate,
            requestEndDate,
            requestId,
            cancellationToken);

        if (overlapsExistingRequest)
        {
            throw new InvalidOperationException("Çalışanın bu dönemle çakışan başka bir izin talebi zaten mevcut.");
        }

        request.EmployeeId = employeeId;
        request.LeaveTypeId = leaveTypeId;
        request.StartDate = requestStartDate;
        request.EndDate = requestEndDate;
        request.RequestedDays = requestedDays;
        request.Reason = trimmedReason;
        request.ManagerApproverEmployeeId = employee.ManagerId;
        request.UpdatedAt = DateTimeOffset.UtcNow;

        var managerApproval = await dbContext.LeaveApprovals.SingleOrDefaultAsync(
            item => item.RequestId == requestId && item.ApproverRole == LeaveApproverRole.Manager,
            cancellationToken);

        if (managerApproval is null)
        {
            managerApproval = new LeaveApproval
            {
                RequestId = requestId,
                ApproverRole = LeaveApproverRole.Manager,
                Decision = LeaveApprovalDecision.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.LeaveApprovals.Add(managerApproval);
        }

        managerApproval.ApproverEmployeeId = employee.ManagerId;

        await auditLogService.AppendAsync(
            AuditActionType.LeaveRequestUpdated,
            nameof(LeaveRequest),
            request.RequestId.ToString(),
            actorUserId,
            $"Status={request.CurrentStatus}; RequestedDays={requestedDays:0.##}",
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return request;
    }

    public async Task ManagerDecisionAsync(
        int requestId,
        int managerEmployeeId,
        bool approve,
        string? comment,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var leaveRequest = await dbContext.LeaveRequests
            .SingleOrDefaultAsync(request => request.RequestId == requestId, cancellationToken);

        if (leaveRequest is null)
        {
            throw new InvalidOperationException("İzin talebi bulunamadı.");
        }

        if (leaveRequest.CurrentStatus != LeaveRequestStatus.ManagerReview)
        {
            throw new InvalidOperationException("İzin talebi yönetici onayı beklemiyor.");
        }

        if (leaveRequest.ManagerApproverEmployeeId != managerEmployeeId)
        {
            throw new InvalidOperationException("Bu onay adımına yalnızca atanmış yönetici karar verebilir.");
        }

        if (approve)
        {
            RecalculateRequestedDays(leaveRequest);
        }

        var approval = await dbContext.LeaveApprovals.SingleOrDefaultAsync(
            item => item.RequestId == requestId && item.ApproverRole == LeaveApproverRole.Manager,
            cancellationToken);

        if (approval is null)
        {
            approval = new LeaveApproval
            {
                RequestId = requestId,
                ApproverRole = LeaveApproverRole.Manager,
                Decision = LeaveApprovalDecision.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.LeaveApprovals.Add(approval);
        }

        ApplyDecision(approval, managerEmployeeId, approve, comment);

        var now = DateTimeOffset.UtcNow;
        leaveRequest.CurrentStatus = approve ? LeaveRequestStatus.HumanResourcesReview : LeaveRequestStatus.Rejected;
        leaveRequest.UpdatedAt = now;

        if (approve)
        {
            dbContext.LeaveApprovals.Add(new LeaveApproval
            {
                RequestId = requestId,
                ApproverRole = LeaveApproverRole.HumanResources,
                Decision = LeaveApprovalDecision.Pending,
                CreatedAt = now
            });
        }

        await auditLogService.AppendAsync(
            approve ? AuditActionType.LeaveRequestManagerApproved : AuditActionType.LeaveRequestManagerRejected,
            nameof(LeaveRequest),
            requestId.ToString(),
            actorUserId,
            comment,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task HumanResourcesDecisionAsync(
        int requestId,
        int humanResourcesEmployeeId,
        bool approve,
        string? comment,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var leaveRequest = await dbContext.LeaveRequests
            .SingleOrDefaultAsync(request => request.RequestId == requestId, cancellationToken);

        if (leaveRequest is null)
        {
            throw new InvalidOperationException("İzin talebi bulunamadı.");
        }

        if (leaveRequest.CurrentStatus != LeaveRequestStatus.HumanResourcesReview)
        {
            throw new InvalidOperationException("İzin talebi insan kaynakları onayı beklemiyor.");
        }

        if (approve)
        {
            RecalculateRequestedDays(leaveRequest);
        }

        var approval = await dbContext.LeaveApprovals.SingleOrDefaultAsync(
            item => item.RequestId == requestId && item.ApproverRole == LeaveApproverRole.HumanResources,
            cancellationToken);

        if (approval is null)
        {
            approval = new LeaveApproval
            {
                RequestId = requestId,
                ApproverRole = LeaveApproverRole.HumanResources,
                Decision = LeaveApprovalDecision.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.LeaveApprovals.Add(approval);
        }
        ApplyDecision(approval, humanResourcesEmployeeId, approve, comment);

        if (approve)
        {
            var requestYear = leaveRequest.StartDate!.Value.Year;
            var balance = await dbContext.LeaveBalances.SingleOrDefaultAsync(
                item => item.EmployeeId == leaveRequest.EmployeeId &&
                        item.LeaveTypeId == leaveRequest.LeaveTypeId &&
                        item.Year == requestYear,
                cancellationToken);

            if (balance is null)
            {
                throw new InvalidOperationException("Talep yılı için izin bakiyesi bulunamadı.");
            }

            if (balance.RemainingDays < leaveRequest.RequestedDays)
            {
                throw new InvalidOperationException("Nihai onay için izin bakiyesi yeterli değil.");
            }

            balance.UsedDays += leaveRequest.RequestedDays;
            balance.RecalculateRemainingDays();
            balance.UpdatedAt = DateTimeOffset.UtcNow;
            leaveRequest.CurrentStatus = LeaveRequestStatus.Approved;
        }
        else
        {
            leaveRequest.CurrentStatus = LeaveRequestStatus.Rejected;
        }

        leaveRequest.UpdatedAt = DateTimeOffset.UtcNow;

        await auditLogService.AppendAsync(
            approve ? AuditActionType.LeaveRequestHumanResourcesApproved : AuditActionType.LeaveRequestHumanResourcesRejected,
            nameof(LeaveRequest),
            requestId.ToString(),
            actorUserId,
            comment,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void ApplyDecision(LeaveApproval approval, int approverEmployeeId, bool approve, string? comment)
    {
        if (!approve && string.IsNullOrWhiteSpace(comment))
        {
            throw new InvalidOperationException("Reddedilen izin taleplerinde gerekçe belirtilmelidir.");
        }

        approval.ApproverEmployeeId = approverEmployeeId;
        approval.Decision = approve ? LeaveApprovalDecision.Approved : LeaveApprovalDecision.Rejected;
        approval.DecisionDate = DateTimeOffset.UtcNow;
        approval.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    private static string RequireText(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }

        return value.Trim();
    }

    private static void ValidateRequestPeriod(DateTime startDate, DateTime endDate)
    {
        if (startDate.Date < DateTime.Today || endDate.Date < DateTime.Today)
        {
            throw new InvalidOperationException("Geçmiş tarihli izin talebi oluşturulamaz.");
        }

        if (endDate.Date < startDate.Date)
        {
            throw new InvalidOperationException("İzin bitiş tarihi, başlangıç tarihinden önce olamaz.");
        }
    }

    private static Employee ValidateEmployeeForRequest(Employee? employee)
    {
        if (employee is null)
        {
            throw new InvalidOperationException("Çalışan bulunamadı.");
        }

        if (employee.Status != EmploymentStatus.Active)
        {
            throw new InvalidOperationException("Pasif çalışanlar izin talebi oluşturamaz.");
        }

        if (employee.ManagerId is null)
        {
            throw new InvalidOperationException("Bir izin talebi başlamadan önce çalışanın bir yöneticisi olmalıdır.");
        }

        return employee;
    }

    private static void ValidateBalanceForRequest(LeaveBalance balance, decimal requestedDays)
    {
        if (balance.RemainingDays < requestedDays)
        {
            throw new InvalidOperationException("Talep edilen dönem için izin bakiyesi yeterli değil.");
        }
    }

    private void RecalculateRequestedDays(LeaveRequest leaveRequest)
    {
        if (leaveRequest.StartDate is null || leaveRequest.EndDate is null)
        {
            throw new InvalidOperationException("Onay için izin talebinin başlangıç ve bitiş tarihleri zorunludur.");
        }

        var startDate = leaveRequest.StartDate.Value.Date;
        var endDate = leaveRequest.EndDate.Value.Date;
        var isHalfDay = leaveRequest.RequestedDays == 0.5m;

        leaveRequest.StartDate = startDate;
        leaveRequest.EndDate = endDate;
        leaveRequest.RequestedDays = dayCalculator.CalculateRequestedDays(
            DateOnly.FromDateTime(startDate),
            DateOnly.FromDateTime(endDate),
            isHalfDay);
    }

    private async Task<bool> HasOverlappingRequestAsync(
        int employeeId,
        DateTime startDate,
        DateTime endDate,
        int? excludedRequestId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.LeaveRequests.Where(request =>
            request.EmployeeId == employeeId &&
            BalanceBlockingStatuses.Contains(request.CurrentStatus) &&
            request.StartDate.HasValue &&
            request.EndDate.HasValue &&
            request.StartDate.Value.Date <= endDate.Date &&
            startDate.Date <= request.EndDate.Value.Date);

        if (excludedRequestId.HasValue)
        {
            query = query.Where(request => request.RequestId != excludedRequestId.Value);
        }

        var overlappingPeriods = await query
            .Select(request => new { request.StartDate, request.EndDate })
            .ToListAsync(cancellationToken);

        var requestedStartDay = DateOnly.FromDateTime(startDate);
        var requestedEndDay = DateOnly.FromDateTime(endDate);

        return overlappingPeriods.Any(period => dayCalculator.HaveOverlappingWorkingDays(
            requestedStartDay,
            requestedEndDay,
            DateOnly.FromDateTime(period.StartDate!.Value),
            DateOnly.FromDateTime(period.EndDate!.Value)));
    }
}
