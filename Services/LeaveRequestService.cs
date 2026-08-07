using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class LeaveRequestService(
    HumanResourcesDbContext dbContext,
    LeaveDayCalculator dayCalculator,
    LeaveEntitlementService entitlementService,
    PublicHolidayCalendar publicHolidayCalendar,
    AuditLogService auditLogService,
    ManagerDelegationService managerDelegationService,
    TimeProvider timeProvider,
    ILogger<LeaveRequestService> logger)
{
    private static readonly LeaveRequestStatus[] BalanceBlockingStatuses =
    [
        LeaveRequestStatus.ManagerReview,
        LeaveRequestStatus.HumanResourcesReview,
        LeaveRequestStatus.Approved
    ];

    public async Task<decimal> CalculateRequestedDaysAsync(
        DateOnly startDate,
        DateOnly endDate,
        bool isHalfDay,
        CancellationToken cancellationToken = default)
    {
        var publicHolidays = await publicHolidayCalendar.GetDatesAsync(
            startDate,
            endDate,
            cancellationToken);

        return dayCalculator.CalculateRequestedDays(
            startDate,
            endDate,
            isHalfDay,
            publicHolidays);
    }

    public async Task<LeaveRequest> CreateRequestAsync(
        int employeeId,
        LeaveRequestCategory category,
        DateTime? startDate,
        DateTime? endDate,
        string reason,
        int actorEmployeeId,
        bool isHalfDay = false,
        CancellationToken cancellationToken = default,
        int? delegateEmployeeId = null,
        int? leaveTypeId = null,
        bool isRetrospective = false)
    {
        EnsureValidSelection(category, leaveTypeId);
        if (startDate is null || endDate is null)
        {
            throw new InvalidOperationException("İzin talebinin başlangıç ve bitiş tarihleri zorunludur.");
        }

        var requestStartDate = startDate.Value.Date;
        var requestEndDate = endDate.Value.Date;
        ValidateRequestPeriod(requestStartDate, requestEndDate, isRetrospective);
        var requestStartDay = requestStartDate.Date;
        var requestEndDay = requestEndDate.Date;
        var trimmedReason = RequireText(reason, "İzin talep nedeni zorunludur.");

        if (requestStartDay.Year != requestEndDay.Year)
        {
            throw new InvalidOperationException("1. aşamada izin talepleri yıl sınırını aşamaz.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

        var requestStartDateOnly = DateOnly.FromDateTime(requestStartDate);
        var requestEndDateOnly = DateOnly.FromDateTime(requestEndDate);
        var publicHolidays = await publicHolidayCalendar.GetDatesAsync(
            requestStartDateOnly,
            requestEndDateOnly,
            cancellationToken);
        var requestedDays = dayCalculator.CalculateRequestedDays(
            requestStartDateOnly,
            requestEndDateOnly,
            isHalfDay,
            publicHolidays);

        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(item => item.EmployeeId == employeeId, cancellationToken);

        var employeeIsDepartmentManager = await dbContext.Departments
            .AsNoTracking()
            .AnyAsync(
                item => item.ManagerEmployeeId == employeeId
                        || item.ActiveDelegateEmployeeId == employeeId,
                cancellationToken);
        employee = ValidateEmployeeForRequest(employee, employeeIsDepartmentManager);
        await ValidateDelegateAsync(employeeId, delegateEmployeeId, actorEmployeeId, cancellationToken);
        var requiresManagerApproval = employee.ManagerId.HasValue;

        var balances = await GetRequestBalancesAsync(
            employeeId,
            category,
            leaveTypeId,
            requestStartDay.Year,
            tracking: false,
            cancellationToken);
        ValidateBalancesForRequest(balances, requestedDays);

        var overlapsExistingRequest = await HasOverlappingRequestAsync(
            employeeId,
            requestStartDate,
            requestEndDate,
            excludedRequestId: null,
            publicHolidays,
            cancellationToken);

        if (overlapsExistingRequest)
        {
            throw new InvalidOperationException("Çalışanın bu dönemle çakışan başka bir izin talebi zaten mevcut.");
        }

        var now = DateTimeOffset.UtcNow;
        var leaveRequest = new LeaveRequest
        {
            EmployeeId = employeeId,
            Category = category,
            LeaveTypeId = leaveTypeId,
            StartDate = requestStartDate,
            EndDate = requestEndDate,
            RequestedDays = requestedDays,
            Reason = trimmedReason,
            IsRetrospective = isRetrospective,
            CurrentStatus = requiresManagerApproval
                ? LeaveRequestStatus.ManagerReview
                : LeaveRequestStatus.HumanResourcesReview,
            ManagerApproverEmployeeId = employee.ManagerId,
            DelegateEmployeeId = delegateEmployeeId,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.LeaveRequests.Add(leaveRequest);
        dbContext.LeaveApprovals.Add(new LeaveApproval
        {
            Request = leaveRequest,
            ApproverRole = LeaveApproverRole.Manager,
            ApproverEmployeeId = employee.ManagerId,
            Decision = requiresManagerApproval
                ? LeaveApprovalDecision.Pending
                : LeaveApprovalDecision.Approved,
            DecisionDate = requiresManagerApproval ? null : now,
            Comment = requiresManagerApproval
                ? null
                : "Üst departmanı bulunmayan yönetici için yönetici onayı uygulanmaz.",
            CreatedAt = now
        });
        if (!requiresManagerApproval)
        {
            dbContext.LeaveApprovals.Add(new LeaveApproval
            {
                Request = leaveRequest,
                ApproverRole = LeaveApproverRole.HumanResources,
                Decision = LeaveApprovalDecision.Pending,
                CreatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.AppendAsync(
            AuditActionType.LeaveRequestCreated,
            nameof(LeaveRequest),
            leaveRequest.RequestId.ToString(),
            actorEmployeeId,
            $"EmployeeId={employeeId}; Category={category}; LeaveTypeId={leaveTypeId?.ToString() ?? "Annual"}; RequestedDays={requestedDays:0.#}; Retrospective={isRetrospective}",
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return leaveRequest;
    }

    public async Task<LeaveRequest> UpdateRequestAsync(
        int requestId,
        int employeeId,
        LeaveRequestCategory category,
        DateTime? startDate,
        DateTime? endDate,
        string reason,
        int actorEmployeeId,
        bool isHalfDay = false,
        CancellationToken cancellationToken = default,
        int? delegateEmployeeId = null,
        int? leaveTypeId = null,
        bool isRetrospective = false)
    {
        EnsureValidSelection(category, leaveTypeId);
        if (startDate is null || endDate is null)
        {
            throw new InvalidOperationException("İzin talebinin başlangıç ve bitiş tarihleri zorunludur.");
        }

        var requestStartDate = startDate.Value.Date;
        var requestEndDate = endDate.Value.Date;
        ValidateRequestPeriod(requestStartDate, requestEndDate, isRetrospective);
        var requestStartDay = requestStartDate.Date;
        var requestEndDay = requestEndDate.Date;
        var trimmedReason = RequireText(reason, "İzin talep nedeni zorunludur.");

        if (requestStartDay.Year != requestEndDay.Year)
        {
            throw new InvalidOperationException("1. aşamada izin talepleri yıl sınırını aşamaz.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

        var requestStartDateOnly = DateOnly.FromDateTime(requestStartDate);
        var requestEndDateOnly = DateOnly.FromDateTime(requestEndDate);
        var publicHolidays = await publicHolidayCalendar.GetDatesAsync(
            requestStartDateOnly,
            requestEndDateOnly,
            cancellationToken);
        var requestedDays = dayCalculator.CalculateRequestedDays(
            requestStartDateOnly,
            requestEndDateOnly,
            isHalfDay,
            publicHolidays);

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

        var employeeIsDepartmentManager = await dbContext.Departments
            .AsNoTracking()
            .AnyAsync(
                item => item.ManagerEmployeeId == employeeId
                        || item.ActiveDelegateEmployeeId == employeeId,
                cancellationToken);
        employee = ValidateEmployeeForRequest(employee, employeeIsDepartmentManager);
        if (request.DelegateEmployeeId != delegateEmployeeId)
        {
            await ValidateDelegateAsync(employeeId, delegateEmployeeId, actorEmployeeId, cancellationToken);
        }

        var balances = await GetRequestBalancesAsync(
            employeeId,
            category,
            leaveTypeId,
            requestStartDay.Year,
            tracking: false,
            cancellationToken);
        ValidateBalancesForRequest(balances, requestedDays);

        var overlapsExistingRequest = await HasOverlappingRequestAsync(
            employeeId,
            requestStartDate,
            requestEndDate,
            requestId,
            publicHolidays,
            cancellationToken);

        if (overlapsExistingRequest)
        {
            throw new InvalidOperationException("Çalışanın bu dönemle çakışan başka bir izin talebi zaten mevcut.");
        }

        request.EmployeeId = employeeId;
        request.Category = category;
        request.LeaveTypeId = leaveTypeId;
        request.StartDate = requestStartDate;
        request.EndDate = requestEndDate;
        request.RequestedDays = requestedDays;
        request.Reason = trimmedReason;
        request.IsRetrospective = isRetrospective;
        request.ManagerApproverEmployeeId = employee.ManagerId;
        request.DelegateEmployeeId = delegateEmployeeId;
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
            actorEmployeeId,
            $"Status={request.CurrentStatus}; RequestedDays={requestedDays:0.##}; Retrospective={isRetrospective}",
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
        int actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

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
            var publicHolidays = await RecalculateRequestedDaysAsync(leaveRequest, cancellationToken);
            await EnsureNoOverlappingRequestAsync(leaveRequest, publicHolidays, cancellationToken);
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
            actorEmployeeId,
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
        int actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

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

        IReadOnlySet<DateOnly>? approvalPublicHolidays = null;
        if (approve)
        {
            approvalPublicHolidays = await RecalculateRequestedDaysAsync(leaveRequest, cancellationToken);
            await EnsureNoOverlappingRequestAsync(
                leaveRequest,
                approvalPublicHolidays,
                cancellationToken);
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
            var balances = await GetRequestBalancesAsync(
                leaveRequest.EmployeeId,
                leaveRequest.Category,
                leaveRequest.LeaveTypeId,
                requestYear,
                tracking: true,
                cancellationToken);
            AllocateApprovedDays(leaveRequest, balances);
            RecordApprovedDays(leaveRequest, approvalPublicHolidays!);
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
            actorEmployeeId,
            comment,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (approve && leaveRequest.DelegateEmployeeId.HasValue)
        {
            try
            {
                var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
                await managerDelegationService.ReconcileAsync(today, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Onaylanan {RequestId} izin talebi için vekâlet hemen uzlaştırılamadı; günlük worker yeniden deneyecek.",
                    requestId);
            }
        }
    }

    private async Task ValidateDelegateAsync(
        int employeeId,
        int? delegateEmployeeId,
        int actorEmployeeId,
        CancellationToken cancellationToken)
    {
        var isDepartmentManager = await dbContext.Departments
            .AsNoTracking()
            .AnyAsync(
                item => item.ManagerEmployeeId == employeeId
                        || item.ActiveDelegateEmployeeId == employeeId,
                cancellationToken);
        if (!isDepartmentManager)
        {
            if (delegateEmployeeId.HasValue)
            {
                throw new InvalidOperationException(
                    "Vekil yalnız departman yöneticisinin izin talebinde seçilebilir.");
            }
            return;
        }

        if (!delegateEmployeeId.HasValue)
        {
            throw new InvalidOperationException(
                "Yönetici izin talebinde vekil seçimi zorunludur.");
        }

        if (actorEmployeeId <= 0)
        {
            throw new InvalidOperationException("Vekâlet işlemi için çalışan kimliği zorunludur.");
        }
        await managerDelegationService.ValidateSelectionAsync(
            employeeId,
            delegateEmployeeId.Value,
            cancellationToken);
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

    private static void ValidateRequestPeriod(
        DateTime startDate,
        DateTime endDate,
        bool isRetrospective)
    {
        if (!isRetrospective
            && (startDate.Date < DateTime.Today || endDate.Date < DateTime.Today))
        {
            throw new InvalidOperationException("Geçmiş tarihli izin talebi oluşturulamaz.");
        }

        if (endDate.Date < startDate.Date)
        {
            throw new InvalidOperationException("İzin bitiş tarihi, başlangıç tarihinden önce olamaz.");
        }
    }

    private static Employee ValidateEmployeeForRequest(
        Employee? employee,
        bool isDepartmentManager)
    {
        if (employee is null)
        {
            throw new InvalidOperationException("Çalışan bulunamadı.");
        }

        if (employee.Status != EmploymentStatus.Active)
        {
            throw new InvalidOperationException("Pasif çalışanlar izin talebi oluşturamaz.");
        }

        if (employee.ManagerId is null && !isDepartmentManager)
        {
            throw new InvalidOperationException("Bir izin talebi başlamadan önce çalışanın bir yöneticisi olmalıdır.");
        }

        return employee;
    }

    public async Task<decimal> GetAvailableDaysAsync(
        int employeeId,
        LeaveRequestCategory category,
        int year,
        CancellationToken cancellationToken = default,
        int? leaveTypeId = null)
    {
        EnsureValidSelection(category, leaveTypeId);
        var balances = await GetRequestBalancesAsync(
            employeeId,
            category,
            leaveTypeId,
            year,
            tracking: false,
            cancellationToken);
        return balances.Sum(balance => balance.RemainingDays);
    }

    private async Task<List<LeaveBalance>> GetRequestBalancesAsync(
        int employeeId,
        LeaveRequestCategory category,
        int? leaveTypeId,
        int year,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = dbContext.LeaveBalances
            .Include(balance => balance.LeaveType)
            .Where(balance => balance.EmployeeId == employeeId && balance.Year == year);
        query = category switch
        {
            LeaveRequestCategory.AnnualLeave => query.Where(balance =>
                balance.LeaveType.EntitlementKind == LeaveEntitlementKind.ServiceYears0To10
                || balance.LeaveType.EntitlementKind == LeaveEntitlementKind.ServiceYears10To20
                || balance.LeaveType.EntitlementKind == LeaveEntitlementKind.ServiceYears20Plus),
            LeaveRequestCategory.SpecificLeaveType => query.Where(balance =>
                balance.LeaveTypeId == leaveTypeId),
            _ => throw new InvalidOperationException("Geçersiz izin talebi kategorisi.")
        };

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var balances = await query
            .OrderBy(balance => balance.LeaveType.EntitlementKind)
            .ThenBy(balance => balance.LeaveTypeId)
            .ToListAsync(cancellationToken);
        if (balances.Count == 0)
        {
            throw new InvalidOperationException(
                "Talep yılı için seçilen izin bakiyesi bulunamadı.");
        }

        if (category == LeaveRequestCategory.SpecificLeaveType)
        {
            var leaveType = balances[0].LeaveType;
            if (LeaveEntitlementService.IsServiceTier(leaveType.EntitlementKind))
            {
                throw new InvalidOperationException(
                    "Kıdem izin türleri yalnız Yıllık İzin seçimi üzerinden kullanılabilir.");
            }

            var employee = await dbContext.Employees
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.EmployeeId == employeeId, cancellationToken)
                ?? throw new InvalidOperationException("Çalışan bulunamadı.");
            if (!entitlementService.Calculate(employee, leaveType, year).Eligible)
            {
                throw new InvalidOperationException(
                    "Çalışan seçilen izin türü için uygun değil.");
            }
        }

        return balances;
    }

    private static void ValidateBalancesForRequest(
        IReadOnlyCollection<LeaveBalance> balances,
        decimal requestedDays)
    {
        ValidateRequestedDays(requestedDays);
        if (balances.Sum(balance => balance.RemainingDays) < requestedDays)
        {
            throw new InvalidOperationException("Talep edilen dönem için izin bakiyesi yeterli değil.");
        }
    }

    private void AllocateApprovedDays(
        LeaveRequest request,
        IReadOnlyList<LeaveBalance> balances)
    {
        ValidateBalancesForRequest(balances, request.RequestedDays);
        var remaining = request.RequestedDays;
        remaining = AllocateFromSource(
            request,
            balances,
            LeaveBalanceAllocationSource.CarryOver,
            remaining);
        remaining = AllocateFromSource(
            request,
            balances,
            LeaveBalanceAllocationSource.Entitlement,
            remaining);
        if (remaining != 0m)
        {
            throw new InvalidOperationException("Nihai onay için izin bakiyesi yeterli değil.");
        }
    }

    private void RecordApprovedDays(
        LeaveRequest request,
        IReadOnlySet<DateOnly> publicHolidays)
    {
        var startDate = DateOnly.FromDateTime(request.StartDate!.Value);
        var endDate = DateOnly.FromDateTime(request.EndDate!.Value);
        var workingDates = dayCalculator.GetWorkingDates(startDate, endDate, publicHolidays);
        var isHalfDay = request.RequestedDays == 0.5m;

        foreach (var workDate in workingDates)
        {
            request.ApprovedDays.Add(new LeaveRequestApprovedDay
            {
                WorkDate = workDate.ToDateTime(TimeOnly.MinValue),
                Days = isHalfDay ? 0.5m : 1m
            });
        }
    }

    private decimal AllocateFromSource(
        LeaveRequest request,
        IReadOnlyList<LeaveBalance> balances,
        LeaveBalanceAllocationSource source,
        decimal remaining)
    {
        foreach (var balance in balances)
        {
            if (remaining == 0m)
            {
                break;
            }

            var capacity = source == LeaveBalanceAllocationSource.CarryOver
                ? balance.CarryOverDays
                : balance.EntitledDays;
            var usedFromSource = source == LeaveBalanceAllocationSource.CarryOver
                ? Math.Min(balance.UsedDays, balance.CarryOverDays)
                : Math.Max(0m, balance.UsedDays - balance.CarryOverDays);
            var available = capacity - usedFromSource;
            var days = Math.Min(remaining, available);
            if (days <= 0m)
            {
                continue;
            }

            dbContext.LeaveRequestBalanceAllocations.Add(new LeaveRequestBalanceAllocation
            {
                Request = request,
                Balance = balance,
                Source = source,
                Days = days,
                CreatedAt = DateTimeOffset.UtcNow
            });
            balance.UsedDays += days;
            balance.RecalculateRemainingDays();
            balance.UpdatedAt = DateTimeOffset.UtcNow;
            remaining -= days;
        }

        return remaining;
    }

    private static void EnsureValidSelection(
        LeaveRequestCategory category,
        int? leaveTypeId)
    {
        if (!Enum.IsDefined(category))
        {
            throw new InvalidOperationException("Geçersiz izin talebi kategorisi.");
        }

        if (category == LeaveRequestCategory.AnnualLeave && leaveTypeId is not null)
        {
            throw new InvalidOperationException(
                "Yıllık İzin seçimi tek bir izin türüne bağlanamaz.");
        }

        if (category == LeaveRequestCategory.SpecificLeaveType
            && (!leaveTypeId.HasValue || leaveTypeId.Value <= 0))
        {
            throw new InvalidOperationException(
                "Seçili izin türü talebinde geçerli bir izin türü zorunludur.");
        }
    }

    private static void ValidateRequestedDays(decimal requestedDays)
    {
        if (requestedDays <= 0m || decimal.Truncate(requestedDays * 2m) != requestedDays * 2m)
        {
            throw new InvalidOperationException(
                "Talep edilen izin yalnız tam ya da yarım gün olabilir.");
        }
    }

    private async Task<IReadOnlySet<DateOnly>> RecalculateRequestedDaysAsync(
        LeaveRequest leaveRequest,
        CancellationToken cancellationToken)
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
        var startDateOnly = DateOnly.FromDateTime(startDate);
        var endDateOnly = DateOnly.FromDateTime(endDate);
        var publicHolidays = await publicHolidayCalendar.GetDatesAsync(
            startDateOnly,
            endDateOnly,
            cancellationToken);
        leaveRequest.RequestedDays = dayCalculator.CalculateRequestedDays(
            startDateOnly,
            endDateOnly,
            isHalfDay,
            publicHolidays);
        return publicHolidays;
    }

    private async Task EnsureNoOverlappingRequestAsync(
        LeaveRequest leaveRequest,
        IReadOnlySet<DateOnly> publicHolidays,
        CancellationToken cancellationToken)
    {
        var overlapsExistingRequest = await HasOverlappingRequestAsync(
            leaveRequest.EmployeeId,
            leaveRequest.StartDate!.Value,
            leaveRequest.EndDate!.Value,
            leaveRequest.RequestId,
            publicHolidays,
            cancellationToken);
        if (overlapsExistingRequest)
        {
            throw new InvalidOperationException(
                "Çalışanın bu dönemle çakışan başka bir izin talebi zaten mevcut.");
        }
    }

    private async Task<bool> HasOverlappingRequestAsync(
        int employeeId,
        DateTime startDate,
        DateTime endDate,
        int? excludedRequestId,
        IReadOnlySet<DateOnly> publicHolidays,
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
            .Select(request => new
            {
                request.StartDate,
                request.EndDate,
                request.CurrentStatus,
                ApprovedDates = request.ApprovedDays
                    .Select(day => day.WorkDate)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var requestedStartDay = DateOnly.FromDateTime(startDate);
        var requestedEndDay = DateOnly.FromDateTime(endDate);
        var requestedWorkingDays = dayCalculator
            .GetWorkingDates(requestedStartDay, requestedEndDay, publicHolidays)
            .ToHashSet();

        return overlappingPeriods.Any(period =>
        {
            var activeDates = period.CurrentStatus == LeaveRequestStatus.Approved
                ? period.ApprovedDates.Select(DateOnly.FromDateTime)
                : dayCalculator.GetWorkingDates(
                    DateOnly.FromDateTime(period.StartDate!.Value),
                    DateOnly.FromDateTime(period.EndDate!.Value),
                    publicHolidays);
            return activeDates.Any(requestedWorkingDays.Contains);
        });
    }
}
