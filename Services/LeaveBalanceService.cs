using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class LeaveBalanceService(HumanResourcesDbContext dbContext, AuditLogService auditLogService)
{
    public async Task<LeaveBalanceOperationResult> RenewAnnualBalanceAsync(
        int employeeId,
        int leaveTypeId,
        int year,
        string actorUserId,
        bool confirmedOverLimit = false,
        CancellationToken cancellationToken = default)
    {
        var leaveType = await EnsureEmployeeAndLeaveTypeExistAsync(employeeId, leaveTypeId, cancellationToken);
        var entitledDays = leaveType.AnnualQuota;

        var previousBalance = await dbContext.LeaveBalances
            .SingleOrDefaultAsync(
                balance => balance.EmployeeId == employeeId &&
                           balance.LeaveTypeId == leaveTypeId &&
                           balance.Year == year - 1,
                cancellationToken);

        var carryOverDays = leaveType.CarryOverRule
            ? previousBalance?.RemainingDays ?? 0m
            : 0m;

        return await UpsertBalanceAsync(
            employeeId,
            leaveTypeId,
            year,
            entitledDays,
            carryOverDays,
            leaveType.MaxAccrualDays,
            AuditActionType.LeaveBalanceRenewed,
            actorUserId,
            confirmedOverLimit,
            cancellationToken);
    }

    public async Task<LeaveBalanceOperationResult> UpdateCarryOverAsync(
        int employeeId,
        int leaveTypeId,
        int year,
        decimal carryOverDays,
        string actorUserId,
        bool confirmedOverLimit = false,
        CancellationToken cancellationToken = default)
    {
        var leaveType = await EnsureEmployeeAndLeaveTypeExistAsync(employeeId, leaveTypeId, cancellationToken);

        if (!leaveType.CarryOverRule && carryOverDays > 0m)
        {
            throw new InvalidOperationException("Bu izin türü devreden gün kullanımına izin vermiyor.");
        }

        var existingBalance = await dbContext.LeaveBalances
            .SingleOrDefaultAsync(
                balance => balance.EmployeeId == employeeId &&
                           balance.LeaveTypeId == leaveTypeId &&
                           balance.Year == year,
                cancellationToken);

        var entitledDays = existingBalance?.EntitledDays ?? 0m;

        return await UpsertBalanceAsync(
            employeeId,
            leaveTypeId,
            year,
            entitledDays,
            carryOverDays,
            leaveType.MaxAccrualDays,
            AuditActionType.LeaveCarryOverUpdated,
            actorUserId,
            confirmedOverLimit,
            cancellationToken);
    }

    private async Task<LeaveBalanceOperationResult> UpsertBalanceAsync(
        int employeeId,
        int leaveTypeId,
        int year,
        decimal entitledDays,
        decimal carryOverDays,
        decimal warningLimitDays,
        AuditActionType actionType,
        string actorUserId,
        bool confirmedOverLimit,
        CancellationToken cancellationToken)
    {
        if (year < 2000)
        {
            throw new InvalidOperationException("İzin bakiyesi yılı 2000 veya sonrası olmalıdır.");
        }

        if (entitledDays < 0m || carryOverDays < 0m)
        {
            throw new InvalidOperationException("İzin günleri negatif olamaz.");
        }

        if (warningLimitDays <= 0m)
        {
            throw new InvalidOperationException("İzin türü için azami birikim günü sıfırdan büyük olmalıdır.");
        }

        var projectedTotalDays = entitledDays + carryOverDays;
        if (projectedTotalDays > warningLimitDays && !confirmedOverLimit)
        {
            return LeaveBalanceOperationResult.ConfirmationRequired(projectedTotalDays, warningLimitDays);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var balance = await dbContext.LeaveBalances
            .SingleOrDefaultAsync(
                item => item.EmployeeId == employeeId &&
                        item.LeaveTypeId == leaveTypeId &&
                        item.Year == year,
                cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (balance is null)
        {
            balance = new LeaveBalance
            {
                EmployeeId = employeeId,
                LeaveTypeId = leaveTypeId,
                Year = year,
                CreatedAt = now
            };

            dbContext.LeaveBalances.Add(balance);
        }

        balance.EntitledDays = entitledDays;
        balance.CarryOverDays = carryOverDays;
        balance.UpdatedAt = now;
        balance.CarryOverLimitWarningConfirmed = projectedTotalDays > warningLimitDays && confirmedOverLimit;
        balance.CarryOverLimitWarningConfirmedAt = balance.CarryOverLimitWarningConfirmed ? now : null;
        balance.CarryOverLimitWarningConfirmedBy = balance.CarryOverLimitWarningConfirmed ? actorUserId : null;
        balance.RecalculateRemainingDays();

        if (balance.RemainingDays < 0m)
        {
            throw new InvalidOperationException("İzin bakiyesi, kullanılan günlerden düşük olamaz.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.AppendAsync(
            actionType,
            nameof(LeaveBalance),
            balance.BalanceId.ToString(),
            actorUserId,
            $"ProjectedTotalDays={projectedTotalDays:0.##}; WarningLimitDays={warningLimitDays:0.##}; ConfirmedOverLimit={confirmedOverLimit}",
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return LeaveBalanceOperationResult.Completed(projectedTotalDays, warningLimitDays, balance);
    }

    private async Task<LeaveType> EnsureEmployeeAndLeaveTypeExistAsync(int employeeId, int leaveTypeId, CancellationToken cancellationToken)
    {
        var employeeExists = await dbContext.Employees.AnyAsync(employee => employee.EmployeeId == employeeId, cancellationToken);
        if (!employeeExists)
        {
            throw new InvalidOperationException("Çalışan bulunamadı.");
        }

        var leaveType = await dbContext.LeaveTypes.SingleOrDefaultAsync(
            leaveType => leaveType.LeaveTypeId == leaveTypeId,
            cancellationToken);

        if (leaveType is null)
        {
            throw new InvalidOperationException("İzin türü bulunamadı.");
        }

        return leaveType;
    }
}
