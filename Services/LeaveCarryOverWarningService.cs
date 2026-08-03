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

    private void EnsureAuthorized(ClaimsPrincipal? principal)
    {
        if (!pageAccessService.CanManageLeaveBalances(principal))
        {
            throw new UnauthorizedAccessException(
                "İzin devir uyarılarını yönetme yetkiniz bulunmuyor.");
        }
    }
}

public sealed record LeaveCarryOverWarningFilter(
    int? Year,
    bool? IsAcknowledged,
    string? EmployeeName,
    string? DepartmentName);
