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
        CancellationToken cancellationToken = default)
    {
        if (!pageAccessService.CanViewAuditLogs(principal))
        {
            throw new UnauthorizedAccessException(
                "Denetim kayıtlarını görüntüleme yetkiniz bulunmuyor.");
        }

        var normalizedPage = Math.Max(0, page);
        var normalizedPageSize = Math.Max(1, pageSize);

        var query = dbContext.AuditLogs.AsNoTracking();
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.ActionDate)
            .ThenByDescending(log => log.AuditLogId)
            .Skip(normalizedPage * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new AuditLogPage(items, totalItems);
    }
}
