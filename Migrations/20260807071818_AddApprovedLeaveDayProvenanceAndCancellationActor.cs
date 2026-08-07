using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedLeaveDayProvenanceAndCancellationActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestedByDisplayName",
                table: "LeaveCancellationRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestedByEmployeeId",
                table: "LeaveCancellationRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeaveRequestApprovedDays",
                columns: table => new
                {
                    ApprovedDayId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateTime>(type: "date", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(2,1)", precision: 2, scale: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequestApprovedDays", x => x.ApprovedDayId);
                    table.CheckConstraint("CK_LeaveRequestApprovedDays_Days", "[Days] IN (0.5, 1.0)");
                    table.ForeignKey(
                        name: "FK_LeaveRequestApprovedDays_LeaveRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "LeaveRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCancellationRequests_RequestedByEmployeeId",
                table: "LeaveCancellationRequests",
                column: "RequestedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestApprovedDays_RequestId_WorkDate",
                table: "LeaveRequestApprovedDays",
                columns: new[] { "RequestId", "WorkDate" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO [LeaveCancellationRequests]
                    ([LeaveRequestId], [CancellationStartDate], [CancellationEndDate],
                     [RequestedRefundDays], [Reason], [IsDirectCancellation], [CurrentStatus],
                     [ManagerApproverEmployeeId], [CreatedAt], [UpdatedAt])
                SELECT request.[RequestId], request.[StartDate], request.[EndDate],
                       request.[RequestedDays],
                       N'Geçiş öncesinde yönetici kararı verilmeden doğrudan iptal edildi.',
                       CAST(1 AS bit), 3, request.[ManagerApproverEmployeeId],
                       request.[UpdatedAt], request.[UpdatedAt]
                FROM [LeaveRequests] AS request
                WHERE request.[CurrentStatus] = 5
                  AND request.[StartDate] IS NOT NULL
                  AND request.[EndDate] IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [LeaveCancellationRequests] AS cancellation
                      WHERE cancellation.[LeaveRequestId] = request.[RequestId]);

                CREATE TABLE #ApprovedDayBackfill
                (
                    [RequestId] int NOT NULL,
                    [WorkDate] date NOT NULL,
                    [Days] decimal(2,1) NOT NULL
                );

                ;WITH ApprovedRequestDates AS
                (
                    SELECT request.[RequestId], request.[StartDate] AS [WorkDate],
                           request.[EndDate], request.[RequestedDays]
                    FROM [LeaveRequests] AS request
                    WHERE request.[CurrentStatus] = 3
                      AND request.[StartDate] IS NOT NULL
                      AND request.[EndDate] IS NOT NULL

                    UNION ALL

                    SELECT dates.[RequestId], DATEADD(day, 1, dates.[WorkDate]),
                           dates.[EndDate], dates.[RequestedDays]
                    FROM ApprovedRequestDates AS dates
                    WHERE dates.[WorkDate] < dates.[EndDate]
                )
                INSERT INTO #ApprovedDayBackfill ([RequestId], [WorkDate], [Days])
                SELECT dates.[RequestId], dates.[WorkDate],
                       CASE WHEN dates.[RequestedDays] = 0.5 THEN 0.5 ELSE 1.0 END
                FROM ApprovedRequestDates AS dates
                WHERE ((DATEDIFF(day, '19000101', dates.[WorkDate]) % 7) + 7) % 7 NOT IN (5, 6)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [PublicHolidays] AS holiday
                      WHERE holiday.[Date] = dates.[WorkDate])
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [LeaveCancellationRequests] AS cancellation
                      WHERE cancellation.[LeaveRequestId] = dates.[RequestId]
                        AND cancellation.[CurrentStatus] = 3
                        AND dates.[WorkDate] BETWEEN cancellation.[CancellationStartDate]
                                                  AND cancellation.[CancellationEndDate])
                OPTION (MAXRECURSION 0);

                IF EXISTS
                (
                    SELECT request.[RequestId]
                    FROM [LeaveRequests] AS request
                    LEFT JOIN #ApprovedDayBackfill AS approvedDay
                        ON approvedDay.[RequestId] = request.[RequestId]
                    WHERE request.[CurrentStatus] = 3
                    GROUP BY request.[RequestId], request.[RequestedDays]
                    HAVING COALESCE(SUM(approvedDay.[Days]), 0) <> request.[RequestedDays]
                )
                BEGIN
                    DROP TABLE #ApprovedDayBackfill;
                    THROW 50003,
                        'Approved leave dates cannot be reconstructed from the current holiday calendar. Resolve the affected DEVELOPMENT rows before applying this migration.',
                        1;
                END;

                INSERT INTO [LeaveRequestApprovedDays] ([RequestId], [WorkDate], [Days])
                SELECT [RequestId], [WorkDate], [Days]
                FROM #ApprovedDayBackfill;

                DROP TABLE #ApprovedDayBackfill;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveCancellationRequests_Employees_RequestedByEmployeeId",
                table: "LeaveCancellationRequests",
                column: "RequestedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId",
                onDelete: ReferentialAction.Restrict);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [LeaveRequestApprovedDays])
                   OR EXISTS (
                       SELECT 1
                       FROM [LeaveCancellationRequests]
                       WHERE [RequestedByEmployeeId] IS NOT NULL
                          OR [RequestedByDisplayName] IS NOT NULL)
                BEGIN
                    THROW 50004,
                        'Rollback would discard approved-day or cancellation-actor provenance. Restore the pre-migration database backup instead.',
                        1;
                END;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveCancellationRequests_Employees_RequestedByEmployeeId",
                table: "LeaveCancellationRequests");

            migrationBuilder.DropTable(
                name: "LeaveRequestApprovedDays");

            migrationBuilder.DropIndex(
                name: "IX_LeaveCancellationRequests_RequestedByEmployeeId",
                table: "LeaveCancellationRequests");

            migrationBuilder.DropColumn(
                name: "RequestedByDisplayName",
                table: "LeaveCancellationRequests");

            migrationBuilder.DropColumn(
                name: "RequestedByEmployeeId",
                table: "LeaveCancellationRequests");

        }
    }
}
