using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class AuditLogPageService(HumanResourcesDbContext dbContext)
{
    public async Task<AuditLogPage> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
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
