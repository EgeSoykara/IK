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

    [Fact]
    public void CancellationWorkflow_HasDurableApprovalAndRefundProvenance()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaContract;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new HumanResourcesDbContext(options);
        var model = dbContext.GetService<IDesignTimeModel>().Model;

        var leaveRequest = model.FindEntityType(typeof(LeaveRequest));
        Assert.NotNull(leaveRequest?.FindProperty(nameof(LeaveRequest.IsRetrospective)));
        Assert.Contains(
            leaveRequest!.GetCheckConstraints(),
            constraint => constraint.Name == "CK_LeaveRequests_CurrentStatus"
                          && constraint.Sql.Contains("5", StringComparison.Ordinal));

        var cancellationRequest = model.FindEntityType(typeof(LeaveCancellationRequest));
        Assert.NotNull(cancellationRequest);
        Assert.Equal("date", cancellationRequest.FindProperty(nameof(LeaveCancellationRequest.ReturnDate))?.GetColumnType());
        Assert.Equal("date", cancellationRequest.FindProperty(nameof(LeaveCancellationRequest.OriginalEndDate))?.GetColumnType());
        Assert.Contains(
            cancellationRequest.GetCheckConstraints(),
            constraint => constraint.Name == "CK_LeaveCancellationRequests_CurrentStatus");

        var cancellationApproval = model.FindEntityType(typeof(LeaveCancellationApproval));
        Assert.NotNull(cancellationApproval);
        Assert.Contains(
            cancellationApproval.GetIndexes(),
            index => index.IsUnique
                     && index.Properties.Select(property => property.Name).SequenceEqual(
                         [nameof(LeaveCancellationApproval.CancellationRequestId), nameof(LeaveCancellationApproval.ApproverRole)]));

        var balanceRefund = model.FindEntityType(typeof(LeaveCancellationBalanceRefund));
        Assert.NotNull(balanceRefund);
        Assert.Contains(
            balanceRefund.GetIndexes(),
            index => index.IsUnique
                     && index.Properties.Select(property => property.Name).SequenceEqual(
                         [nameof(LeaveCancellationBalanceRefund.CancellationRequestId), nameof(LeaveCancellationBalanceRefund.BalanceId), nameof(LeaveCancellationBalanceRefund.Source)]));

        var migrationSource = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "Migrations",
            "20260806105206_AddLeaveCancellationWorkflowAndRetrospectiveRequests.cs"));
        Assert.Contains("sys.check_constraints", migrationSource);
        Assert.Contains("DROP CONSTRAINT [CK_LeaveRequests_CurrentStatus]", migrationSource);
        Assert.Contains("THROW 50001", migrationSource);
    }

    private static string RepoRoot() => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
