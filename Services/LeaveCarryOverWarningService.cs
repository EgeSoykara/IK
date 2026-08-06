using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class LeaveCarryOverWarningService(
    HumanResourcesDbContext dbContext,
    IDbContextFactory<HumanResourcesDbContext> dbContextFactory,
    PageAccessService pageAccessService)
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

    public async Task<LeaveCarryOverReviewResult> ReviewAsync(
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

        await using var writeContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var warning = await writeContext.LeaveCarryOverWarnings
            .Include(item => item.Balance)
            .Include(item => item.LeaveType)
            .SingleOrDefaultAsync(item => item.WarningId == warningId, cancellationToken)
            ?? throw new InvalidOperationException("Devir uyarısı bulunamadı.");
        if (warning.IsAcknowledged)
        {
            throw new InvalidOperationException("Devir uyarısı zaten incelendi.");
        }

        if (!warning.LeaveType.CarryOverRule)
        {
            throw new InvalidOperationException(
                "Bu izin türünde devreden gün düzenlenemez.");
        }

        var balance = warning.Balance;
        var remainingDays = balance.EntitledDays + carryOverDays - balance.UsedDays;
        if (remainingDays < 0m)
        {
            throw new InvalidOperationException(
                "Devreden gün, kullanılan izinler nedeniyle bu değere düşürülemez.");
        }

        writeContext.Entry(warning).Property(item => item.RowVersion).OriginalValue =
            warningRowVersion;
        writeContext.Entry(balance).Property(item => item.RowVersion).OriginalValue =
            balanceRowVersion;

        var oldCarryOverDays = balance.CarryOverDays;
        var oldRemainingDays = balance.RemainingDays;
        var now = DateTimeOffset.UtcNow;
        var carryOverChanged = oldCarryOverDays != carryOverDays;
        balance.CarryOverDays = carryOverDays;
        balance.RemainingDays = remainingDays;
        balance.UpdatedAt = now;

        var totalDays = balance.EntitledDays + carryOverDays;
        var withinLimit = carryOverDays == 0m
                          || totalDays <= warning.LeaveType.MaxAccrualDays;
        balance.CarryOverLimitWarningConfirmed = !withinLimit;
        balance.CarryOverLimitWarningConfirmedAt = withinLimit ? null : now;
        balance.CarryOverLimitWarningConfirmedBy = withinLimit ? null : actorUserId;

        warning.CarryOverDays = carryOverDays;
        warning.EntitledDays = balance.EntitledDays;
        warning.TotalDays = totalDays;
        warning.WarningLimitDays = warning.LeaveType.MaxAccrualDays;
        warning.IsAcknowledged = true;
        warning.AcknowledgedAt = now;
        warning.AcknowledgedBy = actorUserId;
        warning.UpdatedAt = now;

        await new AuditLogService(writeContext).AppendAsync(
            AuditActionType.LeaveCarryOverUpdated,
            nameof(LeaveCarryOverWarning),
            warning.WarningId.ToString(),
            actorUserId,
            $"Reviewed=true; CarryOverDays={oldCarryOverDays:0.#}->{carryOverDays:0.#}; RemainingDays={oldRemainingDays:0.#}->{remainingDays:0.#}; WithinLimit={withinLimit}; Year={warning.Year}",
            cancellationToken);
        await writeContext.SaveChangesAsync(cancellationToken);

        return new LeaveCarryOverReviewResult(carryOverChanged, withinLimit);
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

public sealed record LeaveCarryOverReviewResult(bool CarryOverChanged, bool WithinLimit);

public sealed record LeaveCarryOverWarningFilter(
    int? Year,
    bool? IsAcknowledged,
    string? EmployeeName,
    string? DepartmentName);
