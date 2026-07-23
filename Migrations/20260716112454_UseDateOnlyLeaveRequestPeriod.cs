using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class UseDateOnlyLeaveRequestPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_EmployeeId_StartDate_EndDate",
                table: "LeaveRequests");

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[CK_LeaveRequests_DateRange]', N'C') IS NOT NULL
                    ALTER TABLE [LeaveRequests] DROP CONSTRAINT [CK_LeaveRequests_DateRange];
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "LeaveRequests",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "LeaveRequests",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId_StartDate_EndDate",
                table: "LeaveRequests",
                columns: new[] { "EmployeeId", "StartDate", "EndDate" });

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[CK_LeaveRequests_DateRange]', N'C') IS NULL
                    ALTER TABLE [LeaveRequests] WITH CHECK
                    ADD CONSTRAINT [CK_LeaveRequests_DateRange] CHECK ([EndDate] >= [StartDate]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_EmployeeId_StartDate_EndDate",
                table: "LeaveRequests");

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[CK_LeaveRequests_DateRange]', N'C') IS NOT NULL
                    ALTER TABLE [LeaveRequests] DROP CONSTRAINT [CK_LeaveRequests_DateRange];
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "LeaveRequests",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "LeaveRequests",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId_StartDate_EndDate",
                table: "LeaveRequests",
                columns: new[] { "EmployeeId", "StartDate", "EndDate" });

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[CK_LeaveRequests_DateRange]', N'C') IS NULL
                    ALTER TABLE [LeaveRequests] WITH CHECK
                    ADD CONSTRAINT [CK_LeaveRequests_DateRange] CHECK ([EndDate] >= [StartDate]);
                """);
        }
    }
}
