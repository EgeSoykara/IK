using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

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
        var entityType = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(LeaveRequest));

        Assert.NotNull(entityType);
        Assert.Equal("date", entityType.FindProperty(nameof(LeaveRequest.StartDate))?.GetColumnType());
        Assert.Equal("date", entityType.FindProperty(nameof(LeaveRequest.EndDate))?.GetColumnType());
        Assert.True(entityType.FindProperty(nameof(LeaveRequest.LeaveTypeId))?.IsNullable);
        Assert.Contains(
            entityType.GetCheckConstraints(),
            constraint => constraint.Name == "CK_LeaveRequests_LeaveTypeSelection");

        var leaveType = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(LeaveType));
        Assert.Contains(
            leaveType!.GetCheckConstraints(),
            constraint => constraint.Name == "CK_LeaveTypes_MobilizationPolicy");

        var leaveBalance = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(LeaveBalance));
        Assert.Contains(
            leaveBalance!.GetCheckConstraints(),
            constraint => constraint.Name == "CK_LeaveBalances_MobilizationMaximum");
    }
}
