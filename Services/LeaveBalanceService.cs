using System.Data;
using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IK.Web.Services;

public sealed class LeaveBalanceService(
    HumanResourcesDbContext dbContext,
    AuditLogService auditLogService,
    LeaveEntitlementService entitlementService,
    PageAccessService pageAccessService)
{
    public async Task<LeaveBalanceBatchResult> AssignManualAsync(
        ClaimsPrincipal? principal,
        LeaveBalanceAssignmentRequest request,
        string actorUserId,
        bool confirmedOverLimit = false,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(principal);
        ValidateRequest(request);

        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        var leaveType = await dbContext.LeaveTypes
            .SingleOrDefaultAsync(
                item => item.LeaveTypeId == request.LeaveTypeId,
                cancellationToken)
            ?? throw new InvalidOperationException("İzin türü bulunamadı.");

        var targets = await ResolveTargetsAsync(request, cancellationToken);
        var evaluations = targets
            .Select(employee => new
            {
                Employee = employee,
                Evaluation = entitlementService.Calculate(employee, leaveType, request.Year)
            })
            .ToArray();
        var eligible = evaluations
            .Where(item => item.Evaluation.Eligible)
            .ToArray();
        if (eligible.Length == 0)
        {
            throw new InvalidOperationException(
                "Seçilen kapsamda bu izin türüne uygun aktif çalışan bulunamadı.");
        }

        var employeeIds = eligible
            .Select(item => item.Employee.EmployeeId)
            .ToArray();
        var isServiceTier = IsServiceTier(leaveType.EntitlementKind);
        var relevantLeaveTypeIds = isServiceTier
            ? await dbContext.LeaveTypes
                .Where(item =>
                    item.EntitlementKind == LeaveEntitlementKind.ServiceYears0To10
                    || item.EntitlementKind == LeaveEntitlementKind.ServiceYears10To20
                    || item.EntitlementKind == LeaveEntitlementKind.ServiceYears20Plus)
                .Select(item => item.LeaveTypeId)
                .ToArrayAsync(cancellationToken)
            : [leaveType.LeaveTypeId];
        var previousBalanceRows = await dbContext.LeaveBalances
            .Where(balance =>
                employeeIds.Contains(balance.EmployeeId)
                && relevantLeaveTypeIds.Contains(balance.LeaveTypeId)
                && balance.Year == request.Year - 1)
            .Select(balance => new { balance.EmployeeId, balance.RemainingDays })
            .ToListAsync(cancellationToken);
        var previousBalances = previousBalanceRows
            .GroupBy(balance => balance.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(balance => balance.RemainingDays));
        var currentBalances = await dbContext.LeaveBalances
            .Where(balance =>
                employeeIds.Contains(balance.EmployeeId)
                && balance.LeaveTypeId == leaveType.LeaveTypeId
                && balance.Year == request.Year)
            .ToDictionaryAsync(balance => balance.EmployeeId, cancellationToken);
        var allocationTotals = await LoadAllocationTotalsAsync(
            currentBalances.Values.Select(balance => balance.BalanceId),
            cancellationToken);
        var employeesWithCurrentServiceBalance = isServiceTier
            ? await dbContext.LeaveBalances
                .Where(balance =>
                    employeeIds.Contains(balance.EmployeeId)
                    && relevantLeaveTypeIds.Contains(balance.LeaveTypeId)
                    && balance.Year == request.Year)
                .Select(balance => balance.EmployeeId)
                .Distinct()
                .ToHashSetAsync(cancellationToken)
            : [];

        var projectedMaximum = 0m;
        foreach (var item in eligible)
        {
            var carryOverDays = CalculateManualCarryOver(
                leaveType,
                item.Employee.EmployeeId,
                isServiceTier,
                previousBalances,
                currentBalances,
                employeesWithCurrentServiceBalance);
            projectedMaximum = Math.Max(
                projectedMaximum,
                item.Evaluation.EntitledDays + carryOverDays);
        }

        if (projectedMaximum > leaveType.MaxAccrualDays && !confirmedOverLimit)
        {
            return LeaveBalanceBatchResult.ConfirmationRequired(
                eligible.Length,
                evaluations.Length - eligible.Length,
                projectedMaximum,
                leaveType.MaxAccrualDays);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var item in eligible)
        {
            var carryOverDays = CalculateManualCarryOver(
                leaveType,
                item.Employee.EmployeeId,
                isServiceTier,
                previousBalances,
                currentBalances,
                employeesWithCurrentServiceBalance);
            currentBalances.TryGetValue(item.Employee.EmployeeId, out var balance);
            if (balance is not null)
            {
                EnsureAllocationCapacity(
                    balance,
                    item.Evaluation.EntitledDays,
                    carryOverDays,
                    allocationTotals);
            }
            UpsertBalance(
                balance,
                item.Employee.EmployeeId,
                leaveType,
                request.Year,
                item.Evaluation.EntitledDays,
                carryOverDays,
                actorUserId,
                confirmedOverLimit,
                now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.AppendAsync(
            AuditActionType.LeaveBalanceRenewed,
            nameof(LeaveBalance),
            request.AuditScope,
            actorUserId,
            $"Scope={request.TargetScope}; Assigned={eligible.Length}; Skipped={evaluations.Length - eligible.Length}; LeaveTypeId={leaveType.LeaveTypeId}; Year={request.Year}; ConfirmedOverLimit={confirmedOverLimit}",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);

        return LeaveBalanceBatchResult.Completed(
            eligible.Length,
            evaluations.Length - eligible.Length,
            projectedMaximum,
            leaveType.MaxAccrualDays);
    }

    public async Task<LeaveBalanceOperationResult> UpdateAsync(
        ClaimsPrincipal? principal,
        int balanceId,
        int employeeId,
        int leaveTypeId,
        int year,
        decimal entitledDays,
        decimal carryOverDays,
        byte[] rowVersion,
        string actorUserId,
        bool confirmedOverLimit = false,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(principal);
        ValidateDays(year, entitledDays, carryOverDays);

        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(
                item => item.EmployeeId == employeeId
                        && item.Status == EmploymentStatus.Active,
                cancellationToken)
            ?? throw new InvalidOperationException("Aktif çalışan bulunamadı.");

        var leaveType = await dbContext.LeaveTypes
            .SingleOrDefaultAsync(item => item.LeaveTypeId == leaveTypeId, cancellationToken)
            ?? throw new InvalidOperationException("İzin türü bulunamadı.");
        var evaluation = entitlementService.Calculate(employee, leaveType, year);
        if (!evaluation.Eligible)
        {
            throw new InvalidOperationException(
                "Çalışan seçilen yıl ve izin türü için uygun değil.");
        }

        if (!leaveType.CarryOverRule && carryOverDays > 0m)
        {
            throw new InvalidOperationException("Bu izin türü devreden gün kullanımına izin vermiyor.");
        }

        var balance = await dbContext.LeaveBalances
            .SingleOrDefaultAsync(item => item.BalanceId == balanceId, cancellationToken)
            ?? throw new InvalidOperationException("İzin bakiyesi bulunamadı.");
        var allocationTotals = await LoadAllocationTotalsAsync([balance.BalanceId], cancellationToken);
        if (allocationTotals.Count > 0
            && (balance.EmployeeId != employeeId
                || balance.LeaveTypeId != leaveTypeId
                || balance.Year != year))
        {
            throw new InvalidOperationException(
                "Onaylı izin düşümü bulunan bakiyenin çalışanı, izin türü veya yılı değiştirilemez.");
        }
        EnsureAllocationCapacity(balance, entitledDays, carryOverDays, allocationTotals);
        if (IsServiceTier(leaveType.EntitlementKind) && carryOverDays > 0m)
        {
            await EnsureSingleServiceTierCarryOverAsync(
                balance.BalanceId,
                employee,
                leaveType,
                year,
                cancellationToken);
        }

        var projectedTotalDays = entitledDays + carryOverDays;
        EnsureMobilizationLimit(leaveType, projectedTotalDays);
        if (projectedTotalDays > leaveType.MaxAccrualDays && !confirmedOverLimit)
        {
            return LeaveBalanceOperationResult.ConfirmationRequired(
                projectedTotalDays,
                leaveType.MaxAccrualDays);
        }

        dbContext.Entry(balance)
            .Property(item => item.RowVersion)
            .OriginalValue = rowVersion;

        balance.EmployeeId = employeeId;
        balance.LeaveTypeId = leaveTypeId;
        balance.Year = year;
        ApplyBalanceValues(
            balance,
            entitledDays,
            carryOverDays,
            leaveType.MaxAccrualDays,
            actorUserId,
            confirmedOverLimit,
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.AppendAsync(
            AuditActionType.LeaveBalanceUpdated,
            nameof(LeaveBalance),
            balance.BalanceId.ToString(),
            actorUserId,
            $"EntitledDays={balance.EntitledDays:0.##}; CarryOverDays={balance.CarryOverDays:0.##}; RemainingDays={balance.RemainingDays:0.##}; ConfirmedOverLimit={confirmedOverLimit}",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);

        return LeaveBalanceOperationResult.Completed(
            projectedTotalDays,
            leaveType.MaxAccrualDays,
            balance);
    }

    private async Task EnsureSingleServiceTierCarryOverAsync(
        int balanceId,
        Employee employee,
        LeaveType leaveType,
        int year,
        CancellationToken cancellationToken)
    {
        var serviceLeaveTypes = await dbContext.LeaveTypes
            .Where(item =>
                item.EntitlementKind == LeaveEntitlementKind.ServiceYears0To10
                || item.EntitlementKind == LeaveEntitlementKind.ServiceYears10To20
                || item.EntitlementKind == LeaveEntitlementKind.ServiceYears20Plus)
            .OrderBy(item => item.EntitlementKind)
            .ThenBy(item => item.LeaveTypeId)
            .ToListAsync(cancellationToken);
        var carryOverRecipientId = serviceLeaveTypes
            .Where(item => entitlementService.Calculate(employee, item, year).Eligible)
            .Select(item => (int?)item.LeaveTypeId)
            .FirstOrDefault();
        if (carryOverRecipientId != leaveType.LeaveTypeId)
        {
            throw new InvalidOperationException(
                "Kıdem izinlerinin devreden günleri yalnız yılın ilk uygun kademesinde tutulabilir.");
        }

        var serviceLeaveTypeIds = serviceLeaveTypes
            .Select(item => item.LeaveTypeId)
            .ToArray();
        var anotherCarryOverExists = await dbContext.LeaveBalances
            .AnyAsync(
                item => item.BalanceId != balanceId
                        && item.EmployeeId == employee.EmployeeId
                        && item.Year == year
                        && serviceLeaveTypeIds.Contains(item.LeaveTypeId)
                        && item.CarryOverDays > 0m,
                cancellationToken);
        if (anotherCarryOverExists)
        {
            throw new InvalidOperationException(
                "Kıdem izinlerinin devreden günleri aynı yıl içinde yalnız bir bakiyede tutulabilir.");
        }
    }

    public async Task DeleteAsync(
        ClaimsPrincipal? principal,
        int balanceId,
        byte[] rowVersion,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(principal);
        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);

        var balance = await dbContext.LeaveBalances
            .SingleOrDefaultAsync(item => item.BalanceId == balanceId, cancellationToken)
            ?? throw new InvalidOperationException("İzin bakiyesi bulunamadı.");
        if (await dbContext.LeaveRequestBalanceAllocations
            .AnyAsync(item => item.BalanceId == balanceId, cancellationToken))
        {
            throw new InvalidOperationException(
                "Onaylı izin düşümü bulunan bakiye silinemez.");
        }
        dbContext.Entry(balance)
            .Property(item => item.RowVersion)
            .OriginalValue = rowVersion;
        dbContext.LeaveBalances.Remove(balance);
        await auditLogService.AppendAsync(
            AuditActionType.LeaveBalanceDeleted,
            nameof(LeaveBalance),
            balance.BalanceId.ToString(),
            actorUserId,
            $"LeaveTypeId={balance.LeaveTypeId}; Year={balance.Year}",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
    }

    public async Task<DailyLeaveEntitlementResult> ReconcileAutomaticEntitlementsAsync(
        DateOnly processingDate,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var year = processingDate.Year;
        if (year < 2000)
        {
            throw new InvalidOperationException("İzin bakiyesi yılı 2000 veya sonrası olmalıdır.");
        }

        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        var employees = await dbContext.Employees
            .Where(employee => employee.Status == EmploymentStatus.Active)
            .OrderBy(employee => employee.EmployeeId)
            .ToListAsync(cancellationToken);
        var leaveTypes = await dbContext.LeaveTypes
            .Where(leaveType =>
                leaveType.EntitlementKind == LeaveEntitlementKind.AllEmployees
                || leaveType.EntitlementKind == LeaveEntitlementKind.MaleEmployees
                || leaveType.EntitlementKind == LeaveEntitlementKind.ServiceYears0To10
                || leaveType.EntitlementKind == LeaveEntitlementKind.ServiceYears10To20
                || leaveType.EntitlementKind == LeaveEntitlementKind.ServiceYears20Plus)
            .OrderBy(leaveType => leaveType.EntitlementKind)
            .ThenBy(leaveType => leaveType.LeaveTypeId)
            .ToListAsync(cancellationToken);

        var employeeIds = employees.Select(employee => employee.EmployeeId).ToArray();
        var leaveTypeIds = leaveTypes.Select(leaveType => leaveType.LeaveTypeId).ToArray();
        var currentBalances = await dbContext.LeaveBalances
            .Where(balance =>
                employeeIds.Contains(balance.EmployeeId)
                && leaveTypeIds.Contains(balance.LeaveTypeId)
                && balance.Year == year)
            .ToDictionaryAsync(
                balance => (balance.EmployeeId, balance.LeaveTypeId),
                cancellationToken);
        var allocationTotals = await LoadAllocationTotalsAsync(
            currentBalances.Values.Select(balance => balance.BalanceId),
            cancellationToken);
        var previousBalanceRows = await dbContext.LeaveBalances
            .Where(balance =>
                employeeIds.Contains(balance.EmployeeId)
                && leaveTypeIds.Contains(balance.LeaveTypeId)
                && balance.Year == year - 1)
            .ToListAsync(cancellationToken);
        var previousBalances = previousBalanceRows.ToDictionary(
            balance => (balance.EmployeeId, balance.LeaveTypeId));
        var warningRows = await dbContext.LeaveCarryOverWarnings
            .Where(warning =>
                employeeIds.Contains(warning.EmployeeId)
                && leaveTypeIds.Contains(warning.LeaveTypeId)
                && warning.Year == year)
            .ToDictionaryAsync(
                warning => (warning.EmployeeId, warning.LeaveTypeId),
                cancellationToken);

        var createdCount = 0;
        var updatedCount = 0;
        var unchangedCount = 0;
        var ineligibleCount = 0;
        var warningCount = 0;
        var missingStartDateEmployeeIds = new HashSet<int>();
        var now = DateTimeOffset.UtcNow;

        foreach (var employee in employees)
        {
            var evaluations = leaveTypes
                .Select(leaveType => new
                {
                    LeaveType = leaveType,
                    Evaluation = entitlementService.Calculate(
                        employee,
                        leaveType,
                        year,
                        processingDate)
                })
                .ToArray();
            var serviceCarryOverRecipientId = evaluations
                .Where(item =>
                    item.Evaluation.Eligible
                    && IsServiceTier(item.LeaveType.EntitlementKind))
                .OrderBy(item => item.LeaveType.EntitlementKind)
                .Select(item => (int?)item.LeaveType.LeaveTypeId)
                .FirstOrDefault();
            var hasCurrentServiceBalance = currentBalances.Keys.Any(key =>
                key.EmployeeId == employee.EmployeeId
                && leaveTypes.Any(leaveType =>
                    leaveType.LeaveTypeId == key.LeaveTypeId
                    && IsServiceTier(leaveType.EntitlementKind)));
            var previousServiceCarryOver = previousBalanceRows
                .Where(balance =>
                    balance.EmployeeId == employee.EmployeeId
                    && leaveTypes.Any(leaveType =>
                        leaveType.LeaveTypeId == balance.LeaveTypeId
                        && IsServiceTier(leaveType.EntitlementKind)))
                .Sum(balance => balance.RemainingDays);

            foreach (var item in evaluations)
            {
                var leaveType = item.LeaveType;
                var evaluation = item.Evaluation;
                if (!evaluation.Eligible)
                {
                    ineligibleCount++;
                    if (evaluation.MissingStartDate)
                    {
                        missingStartDateEmployeeIds.Add(employee.EmployeeId);
                    }

                    continue;
                }

                var key = (employee.EmployeeId, leaveType.LeaveTypeId);
                if (currentBalances.TryGetValue(key, out var existingBalance))
                {
                    if (existingBalance.EntitledDays == evaluation.EntitledDays)
                    {
                        unchangedCount++;
                        continue;
                    }

                    EnsureAllocationCapacity(
                        existingBalance,
                        evaluation.EntitledDays,
                        existingBalance.CarryOverDays,
                        allocationTotals);
                    EnsureMobilizationLimit(
                        leaveType,
                        evaluation.EntitledDays + existingBalance.CarryOverDays);
                    ApplyBalanceValues(
                        existingBalance,
                        evaluation.EntitledDays,
                        existingBalance.CarryOverDays,
                        leaveType.MaxAccrualDays,
                        actorUserId,
                        confirmedOverLimit: false,
                        now);
                    updatedCount++;
                    continue;
                }

                var carryOverDays = 0m;
                if (leaveType.CarryOverRule)
                {
                    if (IsServiceTier(leaveType.EntitlementKind))
                    {
                        if (!hasCurrentServiceBalance
                            && serviceCarryOverRecipientId == leaveType.LeaveTypeId)
                        {
                            carryOverDays = previousServiceCarryOver;
                        }
                    }
                    else if (previousBalances.TryGetValue(
                        (employee.EmployeeId, leaveType.LeaveTypeId),
                        out var previous))
                    {
                        carryOverDays = previous.RemainingDays;
                    }
                }
                UpsertBalance(
                    balance: null,
                    employee.EmployeeId,
                    leaveType,
                    year,
                    evaluation.EntitledDays,
                    carryOverDays,
                    actorUserId,
                    confirmedOverLimit: false,
                    now);
                existingBalance = dbContext.LeaveBalances.Local.Single(balance =>
                    balance.EmployeeId == employee.EmployeeId
                    && balance.LeaveTypeId == leaveType.LeaveTypeId
                    && balance.Year == year);
                currentBalances[key] = existingBalance;
                createdCount++;
            }
        }

        if (createdCount > 0 || updatedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var item in currentBalances)
        {
            var balance = item.Value;
            var key = (balance.EmployeeId, balance.LeaveTypeId);
            if (balance.CarryOverDays <= 0m)
            {
                if (warningRows.Remove(key, out var obsoleteWarning))
                {
                    dbContext.LeaveCarryOverWarnings.Remove(obsoleteWarning);
                    warningCount++;
                }
                continue;
            }

            var leaveType = leaveTypes.Single(type => type.LeaveTypeId == balance.LeaveTypeId);
            var totalDays = balance.EntitledDays + balance.CarryOverDays;
            if (totalDays <= leaveType.MaxAccrualDays)
            {
                if (warningRows.Remove(key, out var obsoleteWarning))
                {
                    dbContext.LeaveCarryOverWarnings.Remove(obsoleteWarning);
                    warningCount++;
                }
                continue;
            }

            if (!warningRows.TryGetValue(key, out var warning))
            {
                warning = new LeaveCarryOverWarning
                {
                    BalanceId = balance.BalanceId,
                    EmployeeId = balance.EmployeeId,
                    LeaveTypeId = balance.LeaveTypeId,
                    Year = year,
                    CreatedAt = now
                };
                dbContext.LeaveCarryOverWarnings.Add(warning);
                warningRows[key] = warning;
                warningCount++;
            }
            else if (warning.CarryOverDays != balance.CarryOverDays
                     || warning.EntitledDays != balance.EntitledDays
                     || warning.TotalDays != totalDays
                     || warning.WarningLimitDays != leaveType.MaxAccrualDays)
            {
                warning.IsAcknowledged = false;
                warning.AcknowledgedAt = null;
                warning.AcknowledgedBy = null;
                warningCount++;
            }

            warning.CarryOverDays = balance.CarryOverDays;
            warning.EntitledDays = balance.EntitledDays;
            warning.TotalDays = totalDays;
            warning.WarningLimitDays = leaveType.MaxAccrualDays;
            warning.UpdatedAt = now;
        }

        if (createdCount > 0 || updatedCount > 0 || warningCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.AppendAsync(
                AuditActionType.LeaveBalanceRenewed,
                nameof(LeaveBalance),
                $"Daily:{processingDate:yyyy-MM-dd}",
                actorUserId,
                $"Automatic=true; Date={processingDate:yyyy-MM-dd}; Created={createdCount}; Updated={updatedCount}; Unchanged={unchangedCount}; Ineligible={ineligibleCount}; MissingStartDateEmployees={missingStartDateEmployeeIds.Count}; WarningChanges={warningCount}",
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await CommitAsync(transaction, cancellationToken);
        return new DailyLeaveEntitlementResult(
            processingDate,
            createdCount,
            updatedCount,
            unchangedCount,
            ineligibleCount,
            missingStartDateEmployeeIds.Count,
            warningCount);
    }

    private async Task<Dictionary<(int BalanceId, LeaveBalanceAllocationSource Source), decimal>>
        LoadAllocationTotalsAsync(
            IEnumerable<int> balanceIds,
            CancellationToken cancellationToken)
    {
        var ids = balanceIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return await dbContext.LeaveRequestBalanceAllocations
            .Where(allocation => ids.Contains(allocation.BalanceId))
            .GroupBy(allocation => new { allocation.BalanceId, allocation.Source })
            .Select(group => new
            {
                group.Key.BalanceId,
                group.Key.Source,
                Days = group.Sum(allocation => allocation.Days)
            })
            .ToDictionaryAsync(
                item => (item.BalanceId, item.Source),
                item => item.Days,
                cancellationToken);
    }

    internal static void EnsureAllocationCapacity(
        LeaveBalance balance,
        decimal entitledDays,
        decimal carryOverDays,
        IReadOnlyDictionary<(int BalanceId, LeaveBalanceAllocationSource Source), decimal>
            allocationTotals)
    {
        var allocatedCarryOver = allocationTotals.GetValueOrDefault(
            (balance.BalanceId, LeaveBalanceAllocationSource.CarryOver));
        var allocatedEntitlement = allocationTotals.GetValueOrDefault(
            (balance.BalanceId, LeaveBalanceAllocationSource.Entitlement));
        if (allocatedCarryOver + allocatedEntitlement != balance.UsedDays)
        {
            throw new InvalidOperationException(
                "İzin bakiyesi kullanılan günleriyle düşüm kaynakları tutarlı değil.");
        }

        if (carryOverDays < allocatedCarryOver || entitledDays < allocatedEntitlement)
        {
            throw new InvalidOperationException(
                "İzin bakiyesi, onaylı taleplerde kullanılmış devir veya hak ediş günlerinin altına indirilemez.");
        }
    }

    private async Task<Employee[]> ResolveTargetsAsync(
        LeaveBalanceAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Employees
            .Where(employee => employee.Status == EmploymentStatus.Active);

        query = request.TargetScope switch
        {
            LeaveBalanceTargetScope.Employee when request.EmployeeId.HasValue =>
                query.Where(employee => employee.EmployeeId == request.EmployeeId.Value),
            LeaveBalanceTargetScope.Department when request.DepartmentId.HasValue =>
                query.Where(employee => employee.DepartmentId == request.DepartmentId.Value),
            LeaveBalanceTargetScope.AllEmployees => query,
            LeaveBalanceTargetScope.Employee =>
                throw new InvalidOperationException("Çalışan seçimi zorunludur."),
            LeaveBalanceTargetScope.Department =>
                throw new InvalidOperationException("Departman seçimi zorunludur."),
            _ => throw new InvalidOperationException("Geçersiz bakiye hedef kapsamı.")
        };

        var targets = await query
            .OrderBy(employee => employee.EmployeeId)
            .ToArrayAsync(cancellationToken);
        if (targets.Length == 0)
        {
            throw new InvalidOperationException("Seçilen kapsamda aktif çalışan bulunamadı.");
        }

        return targets;
    }

    private void UpsertBalance(
        LeaveBalance? balance,
        int employeeId,
        LeaveType leaveType,
        int year,
        decimal entitledDays,
        decimal carryOverDays,
        string actorUserId,
        bool confirmedOverLimit,
        DateTimeOffset now)
    {
        EnsureMobilizationLimit(leaveType, entitledDays + carryOverDays);
        if (balance is null)
        {
            balance = new LeaveBalance
            {
                EmployeeId = employeeId,
                LeaveTypeId = leaveType.LeaveTypeId,
                Year = year,
                CreatedAt = now
            };
            dbContext.LeaveBalances.Add(balance);
        }

        ApplyBalanceValues(
            balance,
            entitledDays,
            carryOverDays,
            leaveType.MaxAccrualDays,
            actorUserId,
            confirmedOverLimit,
            now);
    }

    private static void ApplyBalanceValues(
        LeaveBalance balance,
        decimal entitledDays,
        decimal carryOverDays,
        decimal warningLimitDays,
        string actorUserId,
        bool confirmedOverLimit,
        DateTimeOffset now)
    {
        ValidateDays(balance.Year, entitledDays, carryOverDays);
        if (warningLimitDays <= 0m)
        {
            throw new InvalidOperationException(
                "İzin türü için azami birikim günü sıfırdan büyük olmalıdır.");
        }

        if (!IsHalfDayIncrement(warningLimitDays))
        {
            throw new InvalidOperationException(
                "Azami birikim günü yalnız tam ya da yarım gün olabilir.");
        }

        var projectedTotalDays = entitledDays + carryOverDays;
        balance.EntitledDays = entitledDays;
        balance.CarryOverDays = carryOverDays;
        balance.UpdatedAt = now;
        balance.CarryOverLimitWarningConfirmed =
            projectedTotalDays > warningLimitDays && confirmedOverLimit;
        balance.CarryOverLimitWarningConfirmedAt =
            balance.CarryOverLimitWarningConfirmed ? now : null;
        balance.CarryOverLimitWarningConfirmedBy =
            balance.CarryOverLimitWarningConfirmed ? actorUserId : null;
        balance.RecalculateRemainingDays();

        if (balance.RemainingDays < 0m)
        {
            throw new InvalidOperationException(
                "İzin bakiyesi, kullanılan günlerden düşük olamaz.");
        }
    }

    private void EnsureAuthorized(ClaimsPrincipal? principal)
    {
        if (!pageAccessService.CanManageLeaveBalances(principal))
        {
            throw new UnauthorizedAccessException(
                "İzin bakiyesi yönetimi için yetkiniz bulunmuyor.");
        }
    }

    private static void ValidateRequest(LeaveBalanceAssignmentRequest request)
    {
        if (request.LeaveTypeId <= 0)
        {
            throw new InvalidOperationException("İzin türü seçimi zorunludur.");
        }

        if (request.Year < 2000)
        {
            throw new InvalidOperationException("İzin bakiyesi yılı 2000 veya sonrası olmalıdır.");
        }
    }

    private static void ValidateDays(int year, decimal entitledDays, decimal carryOverDays)
    {
        if (year < 2000)
        {
            throw new InvalidOperationException("İzin bakiyesi yılı 2000 veya sonrası olmalıdır.");
        }

        if (entitledDays < 0m || carryOverDays < 0m)
        {
            throw new InvalidOperationException("İzin günleri negatif olamaz.");
        }

        if (!IsHalfDayIncrement(entitledDays) || !IsHalfDayIncrement(carryOverDays))
        {
            throw new InvalidOperationException(
                "İzin günleri yalnız tam ya da yarım gün olabilir.");
        }
    }

    private static bool IsHalfDayIncrement(decimal days) =>
        decimal.Truncate(days * 2m) == days * 2m;

    private static void EnsureMobilizationLimit(LeaveType leaveType, decimal totalDays)
    {
        if (leaveType.EntitlementKind == LeaveEntitlementKind.MaleEmployees
            && totalDays > DomainConstants.MobilizationLeaveMaximumDays)
        {
            throw new InvalidOperationException(
                "Seferberlik İzni bakiyesi toplam 2 günü aşamaz.");
        }
    }

    private static bool IsServiceTier(LeaveEntitlementKind entitlementKind) =>
        entitlementKind is
            LeaveEntitlementKind.ServiceYears0To10
            or LeaveEntitlementKind.ServiceYears10To20
            or LeaveEntitlementKind.ServiceYears20Plus;

    private static decimal CalculateManualCarryOver(
        LeaveType leaveType,
        int employeeId,
        bool isServiceTier,
        IReadOnlyDictionary<int, decimal> previousBalances,
        IReadOnlyDictionary<int, LeaveBalance> currentBalances,
        IReadOnlySet<int> employeesWithCurrentServiceBalance)
    {
        if (!leaveType.CarryOverRule
            || !previousBalances.TryGetValue(employeeId, out var previousRemainingDays))
        {
            return 0m;
        }

        if (!isServiceTier)
        {
            return previousRemainingDays;
        }

        if (currentBalances.TryGetValue(employeeId, out var currentBalance))
        {
            return currentBalance.CarryOverDays;
        }

        return employeesWithCurrentServiceBalance.Contains(employeeId)
            ? 0m
            : previousRemainingDays;
    }

    private async Task<IDbContextTransaction?> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        return dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken)
            : null;
    }

    private static async Task CommitAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }
}

public enum LeaveBalanceTargetScope
{
    Employee = 1,
    Department = 2,
    AllEmployees = 3
}

public sealed record LeaveBalanceAssignmentRequest(
    LeaveBalanceTargetScope TargetScope,
    int? EmployeeId,
    int? DepartmentId,
    int LeaveTypeId,
    int Year)
{
    public string AuditScope => TargetScope switch
    {
        LeaveBalanceTargetScope.Employee => $"Employee:{EmployeeId}",
        LeaveBalanceTargetScope.Department => $"Department:{DepartmentId}",
        LeaveBalanceTargetScope.AllEmployees => "AllEmployees",
        _ => "Unknown"
    };
}

public sealed record LeaveBalanceBatchResult(
    bool Succeeded,
    bool RequiresConfirmation,
    int AssignedCount,
    int SkippedCount,
    decimal ProjectedMaximumDays,
    decimal WarningLimitDays,
    string Message)
{
    public static LeaveBalanceBatchResult ConfirmationRequired(
        int assignedCount,
        int skippedCount,
        decimal projectedMaximumDays,
        decimal warningLimitDays)
    {
        return new LeaveBalanceBatchResult(
            false,
            true,
            assignedCount,
            skippedCount,
            projectedMaximumDays,
            warningLimitDays,
            $"En yüksek projeksiyon {projectedMaximumDays:0.##} gündür ve {warningLimitDays:0.##} günlük uyarı sınırını aşmaktadır. Toplu işlem için onay gereklidir.");
    }

    public static LeaveBalanceBatchResult Completed(
        int assignedCount,
        int skippedCount,
        decimal projectedMaximumDays,
        decimal warningLimitDays)
    {
        return new LeaveBalanceBatchResult(
            true,
            false,
            assignedCount,
            skippedCount,
            projectedMaximumDays,
            warningLimitDays,
            $"{assignedCount} çalışanın izin bakiyesi güncellendi. {skippedCount} çalışan uygunluk kuralı nedeniyle atlandı.");
    }
}

public sealed record DailyLeaveEntitlementResult(
    DateOnly ProcessingDate,
    int CreatedCount,
    int UpdatedCount,
    int UnchangedCount,
    int IneligibleCount,
    int MissingStartDateEmployeeCount,
    int WarningCount);
