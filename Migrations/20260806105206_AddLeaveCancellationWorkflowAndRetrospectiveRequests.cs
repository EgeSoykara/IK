using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveCancellationWorkflowAndRetrospectiveRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs");

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE [name] = N'CK_LeaveRequests_CurrentStatus'
                      AND [parent_object_id] = OBJECT_ID(N'[LeaveRequests]'))
                BEGIN
                    ALTER TABLE [LeaveRequests]
                        DROP CONSTRAINT [CK_LeaveRequests_CurrentStatus];
                END
                """);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetrospective",
                table: "LeaveRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LeaveCancellationRequests",
                columns: table => new
                {
                    CancellationRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaveRequestId = table.Column<int>(type: "int", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "date", nullable: false),
                    OriginalEndDate = table.Column<DateTime>(type: "date", nullable: false),
                    RequestedRefundDays = table.Column<decimal>(type: "decimal(7,1)", precision: 7, scale: 1, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CurrentStatus = table.Column<int>(type: "int", nullable: false),
                    ManagerApproverEmployeeId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveCancellationRequests", x => x.CancellationRequestId);
                    table.CheckConstraint("CK_LeaveCancellationRequests_CurrentStatus", "[CurrentStatus] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_LeaveCancellationRequests_DateRange", "[ReturnDate] <= [OriginalEndDate]");
                    table.CheckConstraint("CK_LeaveCancellationRequests_RefundDays", "[RequestedRefundDays] > 0 AND [RequestedRefundDays] * 2 = FLOOR([RequestedRefundDays] * 2)");
                    table.ForeignKey(
                        name: "FK_LeaveCancellationRequests_Employees_ManagerApproverEmployeeId",
                        column: x => x.ManagerApproverEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveCancellationRequests_LeaveRequests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalTable: "LeaveRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveCancellationApprovals",
                columns: table => new
                {
                    CancellationApprovalId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CancellationRequestId = table.Column<int>(type: "int", nullable: false),
                    ApproverRole = table.Column<int>(type: "int", nullable: false),
                    ApproverEmployeeId = table.Column<int>(type: "int", nullable: true),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    DecisionDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveCancellationApprovals", x => x.CancellationApprovalId);
                    table.CheckConstraint("CK_LeaveCancellationApprovals_ApproverRole", "[ApproverRole] IN (1, 2)");
                    table.CheckConstraint("CK_LeaveCancellationApprovals_Decision", "[Decision] IN (1, 2, 3)");
                    table.CheckConstraint("CK_LeaveCancellationApprovals_RejectionComment", "[Decision] <> 3 OR NULLIF(LTRIM(RTRIM([Comment])), '') IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_LeaveCancellationApprovals_Employees_ApproverEmployeeId",
                        column: x => x.ApproverEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveCancellationApprovals_LeaveCancellationRequests_CancellationRequestId",
                        column: x => x.CancellationRequestId,
                        principalTable: "LeaveCancellationRequests",
                        principalColumn: "CancellationRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeaveCancellationBalanceRefunds",
                columns: table => new
                {
                    RefundId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CancellationRequestId = table.Column<int>(type: "int", nullable: false),
                    BalanceId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(7,1)", precision: 7, scale: 1, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveCancellationBalanceRefunds", x => x.RefundId);
                    table.CheckConstraint("CK_LeaveCancellationBalanceRefunds_Days", "[Days] > 0 AND [Days] * 2 = FLOOR([Days] * 2)");
                    table.CheckConstraint("CK_LeaveCancellationBalanceRefunds_Source", "[Source] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_LeaveCancellationBalanceRefunds_LeaveBalances_BalanceId",
                        column: x => x.BalanceId,
                        principalTable: "LeaveBalances",
                        principalColumn: "BalanceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveCancellationBalanceRefunds_LeaveCancellationRequests_CancellationRequestId",
                        column: x => x.CancellationRequestId,
                        principalTable: "LeaveCancellationRequests",
                        principalColumn: "CancellationRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveRequests_CurrentStatus",
                table: "LeaveRequests",
                sql: "[CurrentStatus] IN (1, 2, 3, 4, 5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs",
                sql: "[ActionType] BETWEEN 1 AND 38");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCancellationApprovals_ApproverEmployeeId",
                table: "LeaveCancellationApprovals",
                column: "ApproverEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCancellationApprovals_CancellationRequestId_ApproverRole",
                table: "LeaveCancellationApprovals",
                columns: new[] { "CancellationRequestId", "ApproverRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCancellationBalanceRefunds_BalanceId",
                table: "LeaveCancellationBalanceRefunds",
                column: "BalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCancellationBalanceRefunds_CancellationRequestId_BalanceId_Source",
                table: "LeaveCancellationBalanceRefunds",
                columns: new[] { "CancellationRequestId", "BalanceId", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCancellationRequests_LeaveRequestId_CurrentStatus",
                table: "LeaveCancellationRequests",
                columns: new[] { "LeaveRequestId", "CurrentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCancellationRequests_ManagerApproverEmployeeId",
                table: "LeaveCancellationRequests",
                column: "ManagerApproverEmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [LeaveRequests]
                    WHERE [CurrentStatus] = 5)
                BEGIN
                    THROW 50001, N'İptal edilmiş izin kayıtları varken migration geri alınamaz; durum 5 için kayıpsız eski şema karşılığı yoktur.', 1;
                END
                """);

            migrationBuilder.DropTable(
                name: "LeaveCancellationApprovals");

            migrationBuilder.DropTable(
                name: "LeaveCancellationBalanceRefunds");

            migrationBuilder.DropTable(
                name: "LeaveCancellationRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveRequests_CurrentStatus",
                table: "LeaveRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IsRetrospective",
                table: "LeaveRequests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveRequests_CurrentStatus",
                table: "LeaveRequests",
                sql: "[CurrentStatus] IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs",
                sql: "[ActionType] BETWEEN 1 AND 32");
        }
    }
}
