using IK.Web.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace IK.Web.Tests;

internal sealed class TestHumanResourcesDbContextFactory(
    DbContextOptions<HumanResourcesDbContext> options)
    : IDbContextFactory<HumanResourcesDbContext>
{
    private int createdContextCount;

    public int CreatedContextCount => Volatile.Read(ref createdContextCount);

    public static TestHumanResourcesDbContextFactory From(
        HumanResourcesDbContext dbContext)
    {
        var options = dbContext.GetService<IDbContextOptions>()
            as DbContextOptions<HumanResourcesDbContext>
            ?? throw new InvalidOperationException(
                "Test veritabanı seçenekleri alınamadı.");
        return new TestHumanResourcesDbContextFactory(options);
    }

    public HumanResourcesDbContext CreateDbContext()
    {
        Interlocked.Increment(ref createdContextCount);
        return new HumanResourcesDbContext(options);
    }

    public Task<HumanResourcesDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDbContext());
    }
}
