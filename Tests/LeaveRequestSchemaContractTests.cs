using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class LeaveRequestSchemaContractTests
{
    [Fact]
    public void LeaveRequestPeriod_UsesDateOnlySqlColumns()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaContract;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new HumanResourcesDbContext(options);
        var entityType = dbContext.Model.FindEntityType(typeof(LeaveRequest));

        Assert.NotNull(entityType);
        Assert.Equal("date", entityType.FindProperty(nameof(LeaveRequest.StartDate))?.GetColumnType());
        Assert.Equal("date", entityType.FindProperty(nameof(LeaveRequest.EndDate))?.GetColumnType());
    }
}
