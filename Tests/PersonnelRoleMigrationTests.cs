using IK.Web.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IK.Web.Tests;

public sealed class PersonnelRoleMigrationTests
{
    private const string ScopedMigration = "20260811074542_ScopePersonnelInformationAccess";
    private const string CompleteCutoverMigration = "20260811075943_CompletePersonnelRoleCutover";

    [Fact]
    public void CompleteCutover_ClassifiesLegacyManagersAndMakesRollbackRepresentable()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=RoleMigrationContract;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new HumanResourcesDbContext(options);
        var migrator = dbContext.GetService<IMigrator>();

        var upScript = migrator.GenerateScript(ScopedMigration, CompleteCutoverMigration);
        var downScript = migrator.GenerateScript(CompleteCutoverMigration, ScopedMigration);

        Assert.Contains("[employee].[ApplicationRoleId] = 2", upScript);
        Assert.Contains("NOT EXISTS", upScript);
        Assert.Contains("[department].[ManagerEmployeeId] = [employee].[EmployeeId]", upScript);
        Assert.Contains("[department].[ActiveDelegateEmployeeId] = [employee].[EmployeeId]", upScript);
        Assert.Contains("SET [ApplicationRoleId] = 4", upScript);

        Assert.Contains("SET [ApplicationRoleId] = 2", downScript);
        Assert.Contains("WHERE [ApplicationRoleId] = 4", downScript);
        Assert.DoesNotContain("[EmployeeId] = 1", downScript);
    }
}
