using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class LeaveCarryOverWarningService(
    HumanResourcesDbContext dbContext,
    PageAccessService pageAccessService,
    AuditLogService auditLogService)
{
    public IQueryable<LeaveCarryOverWarning> AuthorizedQuery(
        ClaimsPrincipal? principal,
        LeaveCarryOverWarningFilter filter)
    {
        EnsureAuthorized(principal);
        var query = dbContext.LeaveCarryOverWarnings
            .AsNoTracking()
            .Include(warning => warning.Balance)
            .Include(warning => warning.Employee)
            .ThenInclude(employee => employee.Department)
            .Include(warning => warning.LeaveType)
            .AsQueryable();

        if (filter.Year.HasValue)
        {
            query = query.Where(warning => warning.Year == filter.Year.Value);
        }

        if (filter.IsAcknowledged.HasValue)
        {
            query = query.Where(warning =>
                warning.IsAcknowledged == filter.IsAcknowledged.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.EmployeeName))
        {
            var employeeName = filter.EmployeeName.Trim();
            query = query.Where(warning =>
                warning.Employee.FirstName.Contains(employeeName)
                || warning.Employee.LastName.Contains(employeeName)
                || (warning.Employee.FirstName + " " + warning.Employee.LastName)
                    .Contains(employeeName));
        }

        if (!string.IsNullOrWhiteSpace(filter.DepartmentName))
        {
            var departmentName = filter.DepartmentName.Trim();
            query = query.Where(warning =>
                warning.Employee.Department.DepartmentName.Contains(departmentName));
        }

        return query;
    }

    public async Task AcknowledgeAsync(
        ClaimsPrincipal? principal,
        int warningId,
        byte[] rowVersion,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(principal);
        var warning = await dbContext.LeaveCarryOverWarnings
            .SingleOrDefaultAsync(item => item.WarningId == warningId, cancellationToken)
            ?? throw new InvalidOperationException("Devir uyarısı bulunamadı.");
        if (warning.IsAcknowledged)
        {
            return;
        }

        dbContext.Entry(warning).Property(item => item.RowVersion).OriginalValue = rowVersion;
        warning.IsAcknowledged = true;
        warning.AcknowledgedAt = DateTimeOffset.UtcNow;
        warning.AcknowledgedBy = actorUserId;
        warning.UpdatedAt = DateTimeOffset.UtcNow;

        await auditLogService.AppendAsync(
            AuditActionType.LeaveCarryOverUpdated,
            nameof(LeaveCarryOverWarning),
            warning.WarningId.ToString(),
            actorUserId,
            $"Acknowledged=true; Year={warning.Year}; CarryOverDays={warning.CarryOverDays:0.#}; TotalDays={warning.TotalDays:0.#}",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<LeaveCarryOverUpdateResult> UpdateCarryOverDaysAsync(
        ClaimsPrincipal? principal,
        int warningId,
        decimal carryOverDays,
        byte[] warningRowVersion,
        byte[] balanceRowVersion,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(principal);
        ValidateCarryOverDays(carryOverDays);

        var warning = await dbContext.LeaveCarryOverWarnings
            .Include(item => item.Balance)
            .Include(item => item.LeaveType)
            .SingleOrDefaultAsync(item => item.WarningId == warningId, cancellationToken)
            ?? throw new InvalidOperationException("Devir uyarısı bulunamadı.");
        if (!warning.LeaveType.CarryOverRule)
        {
            throw new InvalidOperationException(
                "Bu izin türünde devreden gün düzenlenemez.");
        }

        var balance = warning.Balance;
        var allocationTotals = await dbContext.LeaveRequestBalanceAllocations
            .Where(allocation => allocation.BalanceId == balance.BalanceId)
            .GroupBy(allocation => allocation.Source)
            .Select(group => new
            {
                Source = group.Key,
                Days = group.Sum(allocation => allocation.Days)
            })
            .ToDictionaryAsync(
                item => (balance.BalanceId, item.Source),
                item => item.Days,
                cancellationToken);
        LeaveBalanceService.EnsureAllocationCapacity(
            balance,
            balance.EntitledDays,
            carryOverDays,
            allocationTotals);

        var remainingDays = balance.EntitledDays + carryOverDays - balance.UsedDays;
        if (remainingDays < 0m)
        {
            throw new InvalidOperationException(
                "Devreden gün, kullanılan izinler nedeniyle bu değere düşürülemez.");
        }

        if (balance.CarryOverDays == carryOverDays)
        {
            return new LeaveCarryOverUpdateResult(Changed: false, WarningResolved: false);
        }

        dbContext.Entry(warning).Property(item => item.RowVersion).OriginalValue =
            warningRowVersion;
        dbContext.Entry(balance).Property(item => item.RowVersion).OriginalValue =
            balanceRowVersion;

        var oldCarryOverDays = balance.CarryOverDays;
        var oldRemainingDays = balance.RemainingDays;
        var now = DateTimeOffset.UtcNow;
        balance.CarryOverDays = carryOverDays;
        balance.RemainingDays = remainingDays;
        balance.UpdatedAt = now;

        var totalDays = balance.EntitledDays + carryOverDays;
        var warningResolved = carryOverDays == 0m
                              || totalDays <= warning.LeaveType.MaxAccrualDays;
        balance.CarryOverLimitWarningConfirmed = !warningResolved;
        balance.CarryOverLimitWarningConfirmedAt = warningResolved ? null : now;
        balance.CarryOverLimitWarningConfirmedBy = warningResolved ? null : actorUserId;

        if (warningResolved)
        {
            dbContext.LeaveCarryOverWarnings.Remove(warning);
        }
        else
        {
            warning.CarryOverDays = carryOverDays;
            warning.EntitledDays = balance.EntitledDays;
            warning.TotalDays = totalDays;
            warning.WarningLimitDays = warning.LeaveType.MaxAccrualDays;
            warning.IsAcknowledged = false;
            warning.AcknowledgedAt = null;
            warning.AcknowledgedBy = null;
            warning.UpdatedAt = now;
        }

        await auditLogService.AppendAsync(
            AuditActionType.LeaveCarryOverUpdated,
            nameof(LeaveCarryOverWarning),
            warning.WarningId.ToString(),
            actorUserId,
            $"CarryOverDays={oldCarryOverDays:0.#}->{carryOverDays:0.#}; RemainingDays={oldRemainingDays:0.#}->{remainingDays:0.#}; WarningResolved={warningResolved}; Year={warning.Year}",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new LeaveCarryOverUpdateResult(
            Changed: true,
            WarningResolved: warningResolved);
    }

    private void EnsureAuthorized(ClaimsPrincipal? principal)
    {
        if (!pageAccessService.CanManageLeaveBalances(principal))
        {
            throw new UnauthorizedAccessException(
                "İzin devir uyarılarını yönetme yetkiniz bulunmuyor.");
        }
    }

    private static void ValidateCarryOverDays(decimal carryOverDays)
    {
        if (carryOverDays < 0m)
        {
            throw new InvalidOperationException("Devreden gün negatif olamaz.");
        }

        if (decimal.Truncate(carryOverDays * 2m) != carryOverDays * 2m)
        {
            throw new InvalidOperationException(
                "Devreden gün yalnız tam ya da yarım gün olabilir.");
        }
    }
}

public sealed record LeaveCarryOverUpdateResult(bool Changed, bool WarningResolved);

public sealed record LeaveCarryOverWarningFilter(
    int? Year,
    bool? IsAcknowledged,
    string? EmployeeName,
    string? DepartmentName);
