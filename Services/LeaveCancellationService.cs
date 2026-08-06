using System.Data;
using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class LeaveCancellationService(
    HumanResourcesDbContext dbContext,
    LeaveDayCalculator dayCalculator,
    PublicHolidayCalendar publicHolidayCalendar,
    PageAccessService pageAccessService,
    AuditLogService auditLogService,
    ManagerDelegationService managerDelegationService,
    TimeProvider timeProvider,
    ILogger<LeaveCancellationService> logger)
{
    public async Task CancelPendingAsync(
        ClaimsPrincipal? principal,
        int requestId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var request = await dbContext.LeaveRequests
            .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken)
            ?? throw new InvalidOperationException("İzin talebi bulunamadı.");
        EnsureOwnerOrEditor(principal, request.EmployeeId);
        if (request.CurrentStatus != LeaveRequestStatus.ManagerReview)
        {
            throw new InvalidOperationException(
                "Yönetici kararından sonra doğrudan iptal yapılamaz; onay sürecinin sonuçlanması beklenmelidir.");
        }

        var pendingApprovals = await dbContext.LeaveApprovals
            .Where(item => item.RequestId == requestId
                           && item.Decision == LeaveApprovalDecision.Pending)
            .ToListAsync(cancellationToken);
        dbContext.LeaveApprovals.RemoveRange(pendingApprovals);
        request.CurrentStatus = LeaveRequestStatus.Cancelled;
        request.UpdatedAt = timeProvider.GetUtcNow();
        await auditLogService.AppendAsync(
            AuditActionType.LeaveRequestCancelled,
            nameof(LeaveRequest),
            request.RequestId.ToString(),
            actorUserId,
            $"EmployeeId={request.EmployeeId}; RequestedDays={request.RequestedDays:0.##}; Status=Cancelled",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<LeaveCancellationRequest> CreateAsync(
        ClaimsPrincipal? principal,
        int requestId,
        DateTime returnDate,
        string reason,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedReturnDate = returnDate.Date;
        if (normalizedReturnDate < timeProvider.GetLocalNow().Date)
        {
            throw new InvalidOperationException("İşe dönüş tarihi geçmiş bir gün olamaz.");
        }

        var normalizedReason = RequireReason(reason);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var request = await dbContext.LeaveRequests
            .Include(item => item.Employee)
            .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken)
            ?? throw new InvalidOperationException("İzin talebi bulunamadı.");
        EnsureOwnerOrEditor(principal, request.EmployeeId);
        if (request.CurrentStatus != LeaveRequestStatus.Approved)
        {
            throw new InvalidOperationException("Yalnız onaylanmış izin için iptal talebi oluşturulabilir.");
        }
        if (request.StartDate is null || request.EndDate is null)
        {
            throw new InvalidOperationException("İzin tarihleri bulunamadı.");
        }
        if (request.EndDate.Value.Date < timeProvider.GetLocalNow().Date)
        {
            throw new InvalidOperationException("Süresi geçmiş izin için iptal talebi oluşturulamaz.");
        }
        if (normalizedReturnDate < request.StartDate.Value.Date
            || normalizedReturnDate > request.EndDate.Value.Date)
        {
            throw new InvalidOperationException("İşe dönüş tarihi izin tarihleri içinde olmalıdır.");
        }
        if (await dbContext.LeaveCancellationRequests.AnyAsync(
                item => item.LeaveRequestId == requestId
                        && (item.CurrentStatus == LeaveRequestStatus.ManagerReview
                            || item.CurrentStatus == LeaveRequestStatus.HumanResourcesReview),
                cancellationToken))
        {
            throw new InvalidOperationException("Bu izin için sonuçlanmamış bir iptal talebi zaten var.");
        }

        var refundDays = await CalculateRefundDaysAsync(
            request,
            normalizedReturnDate,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var managerApproverEmployeeId = request.Employee.ManagerId;
        var requiresManagerApproval = managerApproverEmployeeId.HasValue;
        var cancellationRequest = new LeaveCancellationRequest
        {
            LeaveRequestId = requestId,
            ReturnDate = normalizedReturnDate,
            OriginalEndDate = request.EndDate.Value.Date,
            RequestedRefundDays = refundDays,
            Reason = normalizedReason,
            CurrentStatus = requiresManagerApproval
                ? LeaveRequestStatus.ManagerReview
                : LeaveRequestStatus.HumanResourcesReview,
            ManagerApproverEmployeeId = managerApproverEmployeeId,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.LeaveCancellationRequests.Add(cancellationRequest);
        cancellationRequest.Approvals.Add(new LeaveCancellationApproval
        {
            ApproverRole = LeaveApproverRole.Manager,
            ApproverEmployeeId = managerApproverEmployeeId,
            Decision = requiresManagerApproval
                ? LeaveApprovalDecision.Pending
                : LeaveApprovalDecision.Approved,
            DecisionDate = requiresManagerApproval ? null : now,
            Comment = requiresManagerApproval
                ? null
                : "Üst yöneticisi bulunmayan çalışan için yönetici onayı uygulanmaz.",
            CreatedAt = now
        });
        if (!requiresManagerApproval)
        {
            cancellationRequest.Approvals.Add(new LeaveCancellationApproval
            {
                ApproverRole = LeaveApproverRole.HumanResources,
                Decision = LeaveApprovalDecision.Pending,
                CreatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.AppendAsync(
            AuditActionType.LeaveCancellationRequested,
            nameof(LeaveCancellationRequest),
            cancellationRequest.CancellationRequestId.ToString(),
            actorUserId,
            $"EmployeeId={request.EmployeeId}; ReturnDate={normalizedReturnDate:yyyy-MM-dd}; EndDate={request.EndDate:yyyy-MM-dd}; RefundDays={refundDays:0.##}",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return cancellationRequest;
    }

    public async Task ManagerDecisionAsync(
        int cancellationRequestId,
        int managerEmployeeId,
        bool approve,
        string? comment,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var request = await dbContext.LeaveCancellationRequests
            .Include(item => item.Approvals)
            .SingleOrDefaultAsync(
                item => item.CancellationRequestId == cancellationRequestId,
                cancellationToken)
            ?? throw new InvalidOperationException("İzin iptal talebi bulunamadı.");
        if (request.CurrentStatus != LeaveRequestStatus.ManagerReview)
        {
            throw new InvalidOperationException("İptal talebi yönetici onayı beklemiyor.");
        }
        if (request.ManagerApproverEmployeeId != managerEmployeeId)
        {
            throw new InvalidOperationException("Bu iptal talebine yalnız atanmış yönetici karar verebilir.");
        }

        var approval = request.Approvals.Single(item => item.ApproverRole == LeaveApproverRole.Manager);
        ApplyDecision(approval, managerEmployeeId, approve, comment);
        request.CurrentStatus = approve
            ? LeaveRequestStatus.HumanResourcesReview
            : LeaveRequestStatus.Rejected;
        request.UpdatedAt = timeProvider.GetUtcNow();
        if (approve)
        {
            request.Approvals.Add(new LeaveCancellationApproval
            {
                ApproverRole = LeaveApproverRole.HumanResources,
                Decision = LeaveApprovalDecision.Pending,
                CreatedAt = timeProvider.GetUtcNow()
            });
        }
        await auditLogService.AppendAsync(
            approve
                ? AuditActionType.LeaveCancellationManagerApproved
                : AuditActionType.LeaveCancellationManagerRejected,
            nameof(LeaveCancellationRequest),
            cancellationRequestId.ToString(),
            actorUserId,
            comment,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task HumanResourcesDecisionAsync(
        int cancellationRequestId,
        int humanResourcesEmployeeId,
        bool approve,
        string? comment,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var cancellationRequest = await dbContext.LeaveCancellationRequests
            .Include(item => item.LeaveRequest)
            .Include(item => item.Approvals)
            .SingleOrDefaultAsync(
                item => item.CancellationRequestId == cancellationRequestId,
                cancellationToken)
            ?? throw new InvalidOperationException("İzin iptal talebi bulunamadı.");
        if (cancellationRequest.CurrentStatus != LeaveRequestStatus.HumanResourcesReview)
        {
            throw new InvalidOperationException("İptal talebi İK onayı beklemiyor.");
        }

        var approval = cancellationRequest.Approvals.Single(
            item => item.ApproverRole == LeaveApproverRole.HumanResources);
        ApplyDecision(approval, humanResourcesEmployeeId, approve, comment);
        if (approve)
        {
            await ApplyRefundAsync(cancellationRequest, cancellationToken);
            cancellationRequest.CurrentStatus = LeaveRequestStatus.Approved;
        }
        else
        {
            cancellationRequest.CurrentStatus = LeaveRequestStatus.Rejected;
        }
        cancellationRequest.UpdatedAt = timeProvider.GetUtcNow();
        await auditLogService.AppendAsync(
            approve
                ? AuditActionType.LeaveCancellationHumanResourcesApproved
                : AuditActionType.LeaveCancellationHumanResourcesRejected,
            nameof(LeaveCancellationRequest),
            cancellationRequestId.ToString(),
            actorUserId,
            approve
                ? $"EmployeeId={cancellationRequest.LeaveRequest.EmployeeId}; ReturnDate={cancellationRequest.ReturnDate:yyyy-MM-dd}; RefundDays={cancellationRequest.RequestedRefundDays:0.##}"
                : comment,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (approve)
        {
            try
            {
                var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
                await managerDelegationService.ReconcileAsync(today, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "İptal edilen {RequestId} izin talebi için vekâlet hemen uzlaştırılamadı.",
                    cancellationRequest.LeaveRequestId);
            }
        }
    }

    private async Task ApplyRefundAsync(
        LeaveCancellationRequest cancellationRequest,
        CancellationToken cancellationToken)
    {
        var leaveRequest = cancellationRequest.LeaveRequest;
        if (leaveRequest.CurrentStatus != LeaveRequestStatus.Approved)
        {
            throw new InvalidOperationException("Yalnız onaylanmış iznin günleri iade edilebilir.");
        }

        var refundDays = await CalculateRefundDaysAsync(
            leaveRequest,
            cancellationRequest.ReturnDate,
            cancellationToken);
        cancellationRequest.RequestedRefundDays = refundDays;
        var priorRefunds = await dbContext.LeaveCancellationBalanceRefunds
            .Where(item => item.CancellationRequest.LeaveRequestId == leaveRequest.RequestId)
            .GroupBy(item => new { item.BalanceId, item.Source })
            .Select(group => new
            {
                group.Key.BalanceId,
                group.Key.Source,
                Days = group.Sum(item => item.Days)
            })
            .ToDictionaryAsync(
                item => (item.BalanceId, item.Source),
                item => item.Days,
                cancellationToken);
        var allocations = await dbContext.LeaveRequestBalanceAllocations
            .Include(item => item.Balance)
            .Where(item => item.RequestId == leaveRequest.RequestId)
            .OrderByDescending(item => item.AllocationId)
            .ToListAsync(cancellationToken);

        var remaining = refundDays;
        foreach (var allocation in allocations)
        {
            if (remaining == 0m) break;
            var alreadyRefunded = priorRefunds.GetValueOrDefault((allocation.BalanceId, allocation.Source));
            var refundable = allocation.Days - alreadyRefunded;
            var days = Math.Min(remaining, refundable);
            if (days <= 0m) continue;
            if (allocation.Balance.UsedDays < days)
            {
                throw new InvalidOperationException(
                    "İzin bakiyesi daha önce düzeltildiği için iptal günleri otomatik iade edilemiyor.");
            }

            allocation.Balance.UsedDays -= days;
            allocation.Balance.RecalculateRemainingDays();
            allocation.Balance.UpdatedAt = timeProvider.GetUtcNow();
            cancellationRequest.BalanceRefunds.Add(new LeaveCancellationBalanceRefund
            {
                BalanceId = allocation.BalanceId,
                Source = allocation.Source,
                Days = days,
                CreatedAt = timeProvider.GetUtcNow()
            });
            remaining -= days;
        }
        if (remaining != 0m)
        {
            throw new InvalidOperationException("İade edilecek izin günlerinin bakiye kaynağı bulunamadı.");
        }

        leaveRequest.UpdatedAt = timeProvider.GetUtcNow();
        if (leaveRequest.RequestedDays == refundDays)
        {
            leaveRequest.CurrentStatus = LeaveRequestStatus.Cancelled;
        }
        else
        {
            leaveRequest.RequestedDays -= refundDays;
            leaveRequest.EndDate = cancellationRequest.ReturnDate.AddDays(-1);
        }
    }

    private async Task<decimal> CalculateRefundDaysAsync(
        LeaveRequest request,
        DateTime returnDate,
        CancellationToken cancellationToken)
    {
        var requestStart = DateOnly.FromDateTime(request.StartDate!.Value);
        var returnDay = DateOnly.FromDateTime(returnDate);
        var usedDays = 0m;
        if (returnDay > requestStart)
        {
            var usedEnd = returnDay.AddDays(-1);
            var holidays = await publicHolidayCalendar.GetDatesAsync(
                requestStart,
                usedEnd,
                cancellationToken);
            usedDays = dayCalculator.CountWorkingDays(
                requestStart,
                usedEnd,
                holidays);
        }

        var refundDays = request.RequestedDays - usedDays;
        if (refundDays <= 0m)
        {
            throw new InvalidOperationException("İşe dönüş tarihinden sonra iade edilecek iş günü bulunmuyor.");
        }
        return refundDays;
    }

    private void EnsureOwnerOrEditor(ClaimsPrincipal? principal, int employeeId)
    {
        var isOwner = principal.GetEmployeeId() == employeeId;
        if (!isOwner && !pageAccessService.CanEditDeleteLeaveRequests(principal))
        {
            throw new UnauthorizedAccessException("Yalnız kendi izin talebinizi iptal edebilirsiniz.");
        }
    }

    private void ApplyDecision(
        LeaveCancellationApproval approval,
        int approverEmployeeId,
        bool approve,
        string? comment)
    {
        if (!approve && string.IsNullOrWhiteSpace(comment))
        {
            throw new InvalidOperationException("Ret gerekçesi zorunludur.");
        }
        approval.ApproverEmployeeId = approverEmployeeId;
        approval.Decision = approve
            ? LeaveApprovalDecision.Approved
            : LeaveApprovalDecision.Rejected;
        approval.DecisionDate = timeProvider.GetUtcNow();
        approval.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    private static string RequireReason(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new InvalidOperationException("İzin iptal nedeni zorunludur.")
            : reason.Trim();
}
