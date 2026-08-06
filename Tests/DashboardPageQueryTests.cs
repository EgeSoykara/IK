using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class DashboardPageQueryTests
{
    [Fact]
    public async Task RelationalQueries_ReturnCurrentBalancesAndBoundedPendingPreview()
    {
        await using var fixture = await DashboardQueryFixture.CreateAsync();
        var database = fixture.Database;
        await fixture.SeedRelationalDataAsync();

        var balances = await DashboardPageQuery.LoadCurrentBalancesAsync(database, 7);
        var pending = await DashboardPageQuery.LoadPendingRequestPreviewsAsync(database, 7);

        Assert.Collection(
            balances,
            balance => Assert.Equal((9002, 2026, 6m), (balance.LeaveTypeId, balance.Year, balance.RemainingDays)),
            balance => Assert.Equal((9001, 2026, 9m), (balance.LeaveTypeId, balance.Year, balance.RemainingDays)));
        Assert.Equal([9204, 9202, 9203], pending.Select(request => request.RequestId));
    }

    [Fact]
    public async Task RequestSummary_ReturnsEmployeeScopedAggregateHistory()
    {
        await using var database = CreateInMemoryDatabase();
        var firstDecisionAt = new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero);
        var latestDecisionAt = firstDecisionAt.AddDays(2);
        database.LeaveRequests.AddRange(
            CreateRequest(1, 7, LeaveRequestStatus.ManagerReview, new DateTime(2026, 9, 4), firstDecisionAt),
            CreateRequest(2, 7, LeaveRequestStatus.HumanResourcesReview, new DateTime(2026, 9, 2), firstDecisionAt),
            CreateRequest(3, 7, LeaveRequestStatus.ManagerReview, new DateTime(2026, 9, 2), firstDecisionAt),
            CreateRequest(4, 7, LeaveRequestStatus.ManagerReview, new DateTime(2026, 9, 1), firstDecisionAt),
            CreateRequest(5, 7, LeaveRequestStatus.Approved, new DateTime(2026, 8, 1), firstDecisionAt),
            CreateRequest(6, 7, LeaveRequestStatus.Rejected, new DateTime(2026, 8, 2), latestDecisionAt),
            CreateRequest(8, 7, LeaveRequestStatus.Cancelled, new DateTime(2026, 8, 3), latestDecisionAt.AddHours(1)),
            CreateRequest(7, 8, LeaveRequestStatus.Approved, new DateTime(2026, 8, 3), latestDecisionAt.AddDays(1)));
        await database.SaveChangesAsync();

        var summary = await DashboardPageQuery.LoadRequestSummaryAsync(database, 7);

        Assert.Equal(4, summary.PendingCount);
        Assert.Equal(3, summary.CompletedCount);
        Assert.Equal(1, summary.ApprovedCount);
        Assert.Equal(1, summary.RejectedCount);
        Assert.Equal(1, summary.CancelledCount);
        Assert.Equal(latestDecisionAt.AddHours(1), summary.LatestCompletedRequestDate);
    }

    [Fact]
    public async Task RequestSummary_WhenEmployeeHasNoRequests_ReturnsEmptySummary()
    {
        await using var database = CreateInMemoryDatabase();

        var summary = await DashboardPageQuery.LoadRequestSummaryAsync(database, employeeId: 404);

        Assert.Equal(DashboardRequestSummary.Empty, summary);
    }

    [Fact]
    public void LeaveRequestLinks_PreserveEmployeeContextAndCreateIntent()
    {
        Assert.Equal("/LeaveRequests?employeeId=42", LeaveRequestPageLink.ForEmployee(42));
        Assert.Equal(
            "/LeaveRequests?employeeId=42&create=true",
            LeaveRequestPageLink.ForEmployee(42, create: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => LeaveRequestPageLink.ForEmployee(0));
    }

    private static HumanResourcesDbContext CreateInMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HumanResourcesDbContext(options);
    }

    private static LeaveRequest CreateRequest(
        int id,
        int employeeId,
        LeaveRequestStatus status,
        DateTime startDate,
        DateTimeOffset updatedAt) => new()
    {
        RequestId = id,
        EmployeeId = employeeId,
        Category = LeaveRequestCategory.AnnualLeave,
        StartDate = startDate,
        EndDate = startDate,
        RequestedDays = 1m,
        Reason = "Test",
        CurrentStatus = status,
        CreatedAt = updatedAt.AddDays(-1),
        UpdatedAt = updatedAt,
        RowVersion = [1]
    };

    private sealed class DashboardQueryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private DashboardQueryFixture(
            SqliteConnection connection,
            HumanResourcesDbContext database)
        {
            this.connection = connection;
            Database = database;
        }

        public HumanResourcesDbContext Database { get; }

        public static async Task<DashboardQueryFixture> CreateAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateCollation(
                "Latin1_General_100_BIN2",
                static (left, right) => string.CompareOrdinal(left, right));
            var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
                .UseSqlite(connection)
                .Options;
            var database = new HumanResourcesDbContext(options);
            await database.Database.EnsureCreatedAsync();
            await database.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            return new DashboardQueryFixture(connection, database);
        }

        public async Task SeedRelationalDataAsync()
        {
            var now = new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero);
            var rowVersion = new byte[] { 1 };
            await Database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO LeaveTypes
                    (LeaveTypeId, Name, AnnualQuota, CarryOverRule, MaxAccrualDays, EntitlementKind)
                VALUES
                    (9001, {"Dashboard Yıllık İzin"}, {30m}, {true}, {50m}, {(int)LeaveEntitlementKind.ServiceYears0To10}),
                    (9002, {"Dashboard Hastalık İzni"}, {30m}, {false}, {50m}, {(int)LeaveEntitlementKind.Manual});
                """);
            await InsertBalanceAsync(9101, 7, 9001, 2025, 4m, now, rowVersion);
            await InsertBalanceAsync(9102, 7, 9001, 2026, 9m, now, rowVersion);
            await InsertBalanceAsync(9103, 7, 9002, 2026, 6m, now, rowVersion);
            await InsertBalanceAsync(9104, 8, 9001, 2027, 99m, now, rowVersion);
            await InsertRequestAsync(9201, 7, LeaveRequestStatus.ManagerReview, new DateTime(2026, 9, 4), now, rowVersion);
            await InsertRequestAsync(9202, 7, LeaveRequestStatus.HumanResourcesReview, new DateTime(2026, 9, 2), now, rowVersion);
            await InsertRequestAsync(9203, 7, LeaveRequestStatus.ManagerReview, new DateTime(2026, 9, 2), now, rowVersion);
            await InsertRequestAsync(9204, 7, LeaveRequestStatus.ManagerReview, new DateTime(2026, 9, 1), now, rowVersion);
            await InsertRequestAsync(9205, 8, LeaveRequestStatus.ManagerReview, new DateTime(2026, 8, 1), now, rowVersion);
        }

        private Task InsertBalanceAsync(
            int id,
            int employeeId,
            int leaveTypeId,
            int year,
            decimal remainingDays,
            DateTimeOffset now,
            byte[] rowVersion) => Database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO LeaveBalances
                    (BalanceId, EmployeeId, LeaveTypeId, Year, EntitledDays, CarryOverDays,
                     UsedDays, RemainingDays, CarryOverLimitWarningConfirmed,
                     CarryOverLimitWarningConfirmedAt, CarryOverLimitWarningConfirmedBy,
                     CreatedAt, UpdatedAt, RowVersion)
                VALUES
                    ({id}, {employeeId}, {leaveTypeId}, {year}, {remainingDays}, {0m}, {0m},
                     {remainingDays}, {false}, NULL, NULL, {now}, {now}, {rowVersion});
                """);

        private Task InsertRequestAsync(
            int id,
            int employeeId,
            LeaveRequestStatus status,
            DateTime startDate,
            DateTimeOffset now,
            byte[] rowVersion) => Database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO LeaveRequests
                    (RequestId, EmployeeId, Category, LeaveTypeId, StartDate, EndDate,
                     RequestedDays, Reason, CurrentStatus, ManagerApproverEmployeeId,
                     DelegateEmployeeId, IsRetrospective, CreatedAt, UpdatedAt, RowVersion)
                VALUES
                    ({id}, {employeeId}, {(int)LeaveRequestCategory.AnnualLeave}, NULL,
                     {startDate}, {startDate}, {1m}, {"Test"}, {(int)status}, NULL, NULL, {false},
                     {now}, {now}, {rowVersion});
                """);

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
