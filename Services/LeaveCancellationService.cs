using System.Data;
using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class LeaveCancellationService(
    HumanResourcesDbContext dbContext,
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
            .Include(item => item.Employee)
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
        if (request.StartDate is null || request.EndDate is null)
        {
            throw new InvalidOperationException("İzin tarihleri bulunamadı.");
        }
        var now = timeProvider.GetUtcNow();
        dbContext.LeaveCancellationRequests.Add(new LeaveCancellationRequest
        {
            LeaveRequestId = request.RequestId,
            CancellationStartDate = request.StartDate.Value.Date,
            CancellationEndDate = request.EndDate.Value.Date,
            RequestedRefundDays = request.RequestedDays,
            Reason = "Talep, yönetici kararı verilmeden doğrudan iptal edildi.",
            IsDirectCancellation = true,
            RequestedByEmployeeId = principal.GetEmployeeId(),
            RequestedByDisplayName = principal?.Identity?.Name ?? actorUserId,
            CurrentStatus = LeaveRequestStatus.Approved,
            ManagerApproverEmployeeId = request.Employee.ManagerId,
            CreatedAt = now,
            UpdatedAt = now
        });
        request.CurrentStatus = LeaveRequestStatus.Cancelled;
        request.UpdatedAt = now;
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
        DateTime cancellationStartDate,
        DateTime cancellationEndDate,
        string reason,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedStartDate = cancellationStartDate.Date;
        var normalizedEndDate = cancellationEndDate.Date;
        if (normalizedEndDate < normalizedStartDate)
        {
            throw new InvalidOperationException("İptal bitiş tarihi, iptal başlangıç tarihinden önce olamaz.");
        }
        if (normalizedStartDate < timeProvider.GetLocalNow().Date)
        {
            throw new InvalidOperationException("Kullanılmış geçmiş izin günleri iptal edilemez.");
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
        if (normalizedStartDate < request.StartDate.Value.Date
            || normalizedEndDate > request.EndDate.Value.Date)
        {
            throw new InvalidOperationException("İptal tarihleri onaylanmış izin tarihleri içinde olmalıdır.");
        }
        if (await dbContext.LeaveCancellationRequests.AnyAsync(
                item => item.LeaveRequestId == requestId
                        && item.CurrentStatus != LeaveRequestStatus.Rejected
                        && item.CancellationStartDate <= normalizedEndDate
                        && normalizedStartDate <= item.CancellationEndDate,
                cancellationToken))
        {
            throw new InvalidOperationException("Seçilen günlerin tümü veya bir kısmı için zaten iptal kaydı var.");
        }

        var refundDays = await CalculateRefundDaysAsync(
            request,
            normalizedStartDate,
            normalizedEndDate,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var managerApproverEmployeeId = request.Employee.ManagerId;
        var requiresManagerApproval = managerApproverEmployeeId.HasValue;
        var cancellationRequest = new LeaveCancellationRequest
        {
            LeaveRequestId = requestId,
            CancellationStartDate = normalizedStartDate,
            CancellationEndDate = normalizedEndDate,
            RequestedRefundDays = refundDays,
            Reason = normalizedReason,
            RequestedByEmployeeId = principal.GetEmployeeId(),
            RequestedByDisplayName = principal?.Identity?.Name ?? actorUserId,
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
            $"EmployeeId={request.EmployeeId}; CancellationStartDate={normalizedStartDate:yyyy-MM-dd}; CancellationEndDate={normalizedEndDate:yyyy-MM-dd}; RefundDays={refundDays:0.##}",
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
                ? $"EmployeeId={cancellationRequest.LeaveRequest.EmployeeId}; CancellationStartDate={cancellationRequest.CancellationStartDate:yyyy-MM-dd}; CancellationEndDate={cancellationRequest.CancellationEndDate:yyyy-MM-dd}; RefundDays={cancellationRequest.RequestedRefundDays:0.##}"
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
            cancellationRequest.CancellationStartDate,
            cancellationRequest.CancellationEndDate,
            cancellationToken);
        cancellationRequest.RequestedRefundDays = refundDays;
        var approvedDays = await dbContext.LeaveRequestApprovedDays
            .Where(item => item.RequestId == leaveRequest.RequestId
                           && item.WorkDate >= cancellationRequest.CancellationStartDate
                           && item.WorkDate <= cancellationRequest.CancellationEndDate)
            .ToListAsync(cancellationToken);
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

        dbContext.LeaveRequestApprovedDays.RemoveRange(approvedDays);

        leaveRequest.UpdatedAt = timeProvider.GetUtcNow();
        if (leaveRequest.RequestedDays == refundDays)
        {
            leaveRequest.CurrentStatus = LeaveRequestStatus.Cancelled;
        }
        else
        {
            leaveRequest.RequestedDays -= refundDays;
        }
    }

    private async Task<decimal> CalculateRefundDaysAsync(
        LeaveRequest request,
        DateTime cancellationStartDate,
        DateTime cancellationEndDate,
        CancellationToken cancellationToken)
    {
        var refundDays = await dbContext.LeaveRequestApprovedDays
            .Where(item => item.RequestId == request.RequestId
                           && item.WorkDate >= cancellationStartDate.Date
                           && item.WorkDate <= cancellationEndDate.Date)
            .SumAsync(item => (decimal?)item.Days, cancellationToken)
            ?? 0m;
        if (refundDays <= 0m)
        {
            throw new InvalidOperationException(
                "Seçilen iptal aralığında onaylanmış ve iade edilebilecek izin günü bulunmuyor.");
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
