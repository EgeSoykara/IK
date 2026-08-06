using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IK.Web.Services;

public sealed class AuditLogPageService(
    HumanResourcesDbContext dbContext,
    PageAccessService pageAccessService)
{
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

        var query = dbContext.AuditLogs.AsNoTracking();
        if (normalizedCriteria.UserId is { } userId)
        {
            query = query.Where(log => log.UserId.Contains(userId));
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

        return new AuditLogPage(items, totalItems);
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
