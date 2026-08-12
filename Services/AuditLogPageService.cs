using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IK.Web.Services;

public sealed class AuditLogPageService(
    IDbContextFactory<HumanResourcesDbContext> dbContextFactory,
    PageAccessService pageAccessService)
{
    public async Task<AuditActorOption?> GetEmployeeOptionAsync(
        ClaimsPrincipal? principal,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        if (!pageAccessService.CanViewAuditLogs(principal))
        {
            throw new UnauthorizedAccessException(
                "Denetim kayıtlarını görüntüleme yetkiniz bulunmuyor.");
        }

        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await database.Employees
            .AsNoTracking()
            .Where(employee => employee.EmployeeId == employeeId)
            .Select(employee => new AuditActorOption(
                employee.EmployeeId,
                employee.FirstName + " " + employee.LastName,
                employee.SicilNo))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditActorOption>> SearchEmployeesAsync(
        ClaimsPrincipal? principal,
        string? searchText,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!pageAccessService.CanViewAuditLogs(principal))
        {
            throw new UnauthorizedAccessException(
                "Denetim kayıtlarını görüntüleme yetkiniz bulunmuyor.");
        }

        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = database.Employees
            .AsNoTracking()
            .Where(employee => database.AuditLogs.Any(
                log => log.ActorEmployeeId == employee.EmployeeId));
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var normalized = searchText.Trim();
            query = query.Where(employee =>
                employee.FirstName.Contains(normalized)
                || employee.LastName.Contains(normalized)
                || employee.SicilNo.Contains(normalized)
                || (employee.FirstName + " " + employee.LastName).Contains(normalized));
        }

        return await query
            .OrderBy(employee => employee.FirstName)
            .ThenBy(employee => employee.LastName)
            .ThenBy(employee => employee.EmployeeId)
            .Select(employee => new AuditActorOption(
                employee.EmployeeId,
                employee.FirstName + " " + employee.LastName,
                employee.SicilNo))
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);
    }

    public async Task<AuditLogPage> GetPageAsync(
        ClaimsPrincipal? principal,
        int page,
        int pageSize,
        AuditLogSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (!pageAccessService.CanViewAuditLogs(principal))
        {
            throw new UnauthorizedAccessException(
                "Denetim kayıtlarını görüntüleme yetkiniz bulunmuyor.");
        }

        var normalizedPage = Math.Max(0, page);
        var normalizedPageSize = Math.Max(1, pageSize);

        var normalizedCriteria = criteria.Normalize();
        if (!normalizedCriteria.HasAny)
        {
            return new AuditLogPage([], 0);
        }

        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = database.AuditLogs.AsNoTracking();
        if (normalizedCriteria.ActorEmployeeId is { } actorEmployeeId)
        {
            query = query.Where(log => log.ActorEmployeeId == actorEmployeeId);
        }
        if (normalizedCriteria.ActionType is { } actionType)
        {
            query = query.Where(log => log.ActionType == actionType);
        }
        if (normalizedCriteria.StartDate is { } startDate)
        {
            var start = ToUtcBoundary(startDate);
            query = query.Where(log => log.ActionDate >= start);
        }
        if (normalizedCriteria.EndDate is { } endDate)
        {
            var endExclusive = ToUtcBoundary(endDate.AddDays(1));
            query = query.Where(log => log.ActionDate < endExclusive);
        }
        if (normalizedCriteria.Text is { } text)
        {
            query = query.Where(log => log.Details != null && log.Details.Contains(text));
        }
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.ActionDate)
            .ThenByDescending(log => log.AuditLogId)
            .Skip(normalizedPage * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var employeeIds = items
            .Where(log => log.ActorEmployeeId.HasValue)
            .Select(log => log.ActorEmployeeId!.Value)
            .Distinct()
            .ToArray();
        var employeeNames = await database.Employees
            .AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.EmployeeId))
            .ToDictionaryAsync(
                employee => employee.EmployeeId,
                employee => employee.FirstName + " " + employee.LastName,
                cancellationToken);
        foreach (var item in items)
        {
            item.ActorDisplayName = ResolveActorDisplayName(item, employeeNames);
        }

        return new AuditLogPage(items, totalItems);
    }

    private static string ResolveActorDisplayName(
        AuditLog auditLog,
        IReadOnlyDictionary<int, string> employeeNames)
    {
        if (auditLog.ActorEmployeeId is { } employeeId)
        {
            return employeeNames.TryGetValue(employeeId, out var displayName)
                ? displayName
                : $"Silinmiş çalışan (#{employeeId})";
        }

        return auditLog.SystemActorKey switch
        {
            DailyLeaveEntitlementWorker.SystemActor => "Günlük İzin Otomasyonu",
            SystemActorKeys.InitialConfiguration => "İlk Kurulum",
            SystemActorKeys.ManagerRoleBackfill => "Yönetici Rolü Geçişi",
            _ => "Sistem"
        };
    }

    private static DateTimeOffset ToUtcBoundary(DateOnly date)
    {
        var localDateTime = DateTime.SpecifyKind(
            date.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        return new DateTimeOffset(
                localDateTime,
                TimeZoneInfo.Local.GetUtcOffset(localDateTime))
            .ToUniversalTime();
    }
}
