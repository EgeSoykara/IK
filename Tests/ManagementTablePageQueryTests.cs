using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class ManagementTablePageQueryTests
{
    [Fact]
    public async Task ToTableDataAsync_ReturnsSecondPageWithUnpagedTotalItems()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Departments.AddRange(Enumerable.Range(1, 60).Select(id => new Department
        {
            DepartmentId = id,
            DepartmentName = $"Department {id:00}"
        }));
        await dbContext.SaveChangesAsync();

        var page = await dbContext.Departments
            .AsNoTracking()
            .OrderByDescending(department => department.DepartmentId)
            .ToTableDataAsync(page: 1, pageSize: 25);

        Assert.Equal(60, page.TotalItems);
        Assert.Equal(DescendingIds(11, 25), (page.Items ?? []).Select(department => department.DepartmentId));
    }

    [Fact]
    public async Task ToTableDataAsync_CountsFilteredItemsBeforePaging()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Departments.AddRange(Enumerable.Range(1, 40).Select(id => new Department
        {
            DepartmentId = id,
            DepartmentName = id % 2 == 0 ? $"Paged {id:00}" : $"Other {id:00}"
        }));
        await dbContext.SaveChangesAsync();

        var page = await dbContext.Departments
            .AsNoTracking()
            .Where(department => department.DepartmentName.StartsWith("Paged"))
            .OrderByDescending(department => department.DepartmentId)
            .ToTableDataAsync(page: 1, pageSize: 7);

        Assert.Equal(20, page.TotalItems);
        Assert.Equal([26, 24, 22, 20, 18, 16, 14], (page.Items ?? []).Select(department => department.DepartmentId));
    }

    [Fact]
    public async Task ToTableDataAsync_NormalizesInvalidPageInputs()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Departments.AddRange(Enumerable.Range(1, 3).Select(id => new Department
        {
            DepartmentId = id,
            DepartmentName = $"Department {id:00}"
        }));
        await dbContext.SaveChangesAsync();

        var page = await dbContext.Departments
            .AsNoTracking()
            .OrderBy(department => department.DepartmentId)
            .ToTableDataAsync(page: -5, pageSize: 0);

        Assert.Equal(3, page.TotalItems);
        Assert.Equal([1], (page.Items ?? []).Select(department => department.DepartmentId));
    }

    private static IEnumerable<int> DescendingIds(int start, int count)
    {
        return Enumerable.Range(start, count).Reverse();
    }

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HumanResourcesDbContext(options);
    }
}
