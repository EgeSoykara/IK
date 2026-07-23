using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace IK.Web.Services;

public static class ManagementTablePageQuery
{
    public static async Task<TableData<TItem>> ToTableDataAsync<TItem>(
        this IQueryable<TItem> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(0, page);
        var normalizedPageSize = Math.Max(1, pageSize);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(normalizedPage * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new TableData<TItem>
        {
            Items = items,
            TotalItems = totalItems
        };
    }
}
