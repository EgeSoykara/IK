using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class DailyLeaveEntitlementsAndCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_LeaveTypes_LeaveTypeId",
                table: "LeaveRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveBalances_LeaveTypes_LeaveTypeId",
                table: "LeaveBalances");

            // DEVELOPMENT clean cutover: the old per-type request/balance authority is
            // intentionally reset instead of converted. Employee and department records
            // remain intact; derived leave approvals, delegations and leave audit history
            // are removed with the obsolete leave data.
            migrationBuilder.Sql(
                """
                UPDATE Departments
                SET ActiveDelegateEmployeeId = NULL
                WHERE ActiveDelegateEmployeeId IS NOT NULL;

                DELETE FROM ManagerDelegations;
                DELETE FROM LeaveApprovals;
                DELETE FROM LeaveRequests;
                DELETE FROM LeaveBalances;
                DELETE FROM AuditLogs
                WHERE EntityName IN ('LeaveType', 'LeaveBalance', 'LeaveRequest', 'ManagerDelegation');
                DELETE FROM LeaveTypes;

                DBCC CHECKIDENT ('ManagerDelegations', RESEED, 0) WITH NO_INFOMSGS;
                DBCC CHECKIDENT ('LeaveApprovals', RESEED, 0) WITH NO_INFOMSGS;
                DBCC CHECKIDENT ('LeaveRequests', RESEED, 0) WITH NO_INFOMSGS;
                DBCC CHECKIDENT ('LeaveBalances', RESEED, 0) WITH NO_INFOMSGS;
                """);

            migrationBuilder.RenameColumn(
                name: "LeaveTypeId",
                table: "LeaveRequests",
                newName: "Category");

            migrationBuilder.RenameIndex(
                name: "IX_LeaveRequests_LeaveTypeId",
                table: "LeaveRequests",
                newName: "IX_LeaveRequests_Category");

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxAccrualDays",
                table: "LeaveTypes",
                type: "decimal(7,1)",
                precision: 7,
                scale: 1,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "AnnualQuota",
                table: "LeaveTypes",
                type: "decimal(7,1)",
                precision: 7,
                scale: 1,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "RequestedDays",
                table: "LeaveRequests",
                type: "decimal(7,1)",
                precision: 7,
                scale: 1,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "UsedDays",
                table: "LeaveBalances",
                type: "decimal(7,1)",
                precision: 7,
                scale: 1,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "RemainingDays",
                table: "LeaveBalances",
                type: "decimal(7,1)",
                precision: 7,
                scale: 1,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "EntitledDays",
                table: "LeaveBalances",
                type: "decimal(7,1)",
                precision: 7,
                scale: 1,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "CarryOverDays",
                table: "LeaveBalances",
                type: "decimal(7,1)",
                precision: 7,
                scale: 1,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2);

            migrationBuilder.CreateTable(
                name: "LeaveCarryOverWarnings",
                columns: table => new
                {
                    WarningId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BalanceId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    CarryOverDays = table.Column<decimal>(type: "decimal(7,1)", precision: 7, scale: 1, nullable: false),
                    EntitledDays = table.Column<decimal>(type: "decimal(7,1)", precision: 7, scale: 1, nullable: false),
                    TotalDays = table.Column<decimal>(type: "decimal(7,1)", precision: 7, scale: 1, nullable: false),
                    WarningLimitDays = table.Column<decimal>(type: "decimal(7,1)", precision: 7, scale: 1, nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveCarryOverWarnings", x => x.WarningId);
                    table.CheckConstraint("CK_LeaveCarryOverWarnings_HalfDayAmounts", "[CarryOverDays] >= 0 AND [CarryOverDays] * 2 = FLOOR([CarryOverDays] * 2) AND [EntitledDays] >= 0 AND [EntitledDays] * 2 = FLOOR([EntitledDays] * 2) AND [TotalDays] >= 0 AND [TotalDays] * 2 = FLOOR([TotalDays] * 2) AND [WarningLimitDays] > 0 AND [WarningLimitDays] * 2 = FLOOR([WarningLimitDays] * 2) AND [TotalDays] = [EntitledDays] + [CarryOverDays]");
                    table.ForeignKey(
                        name: "FK_LeaveCarryOverWarnings_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveCarryOverWarnings_LeaveBalances_BalanceId",
                        column: x => x.BalanceId,
                        principalTable: "LeaveBalances",
                        principalColumn: "BalanceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaveCarryOverWarnings_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "LeaveTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequestBalanceAllocations",
                columns: table => new
                {
                    AllocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    BalanceId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(7,1)", precision: 7, scale: 1, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequestBalanceAllocations", x => x.AllocationId);
                    table.CheckConstraint("CK_LeaveRequestBalanceAllocations_HalfDayAmount", "[Days] > 0 AND [Days] * 2 = FLOOR([Days] * 2)");
                    table.CheckConstraint("CK_LeaveRequestBalanceAllocations_Source", "[Source] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_LeaveRequestBalanceAllocations_LeaveBalances_BalanceId",
                        column: x => x.BalanceId,
                        principalTable: "LeaveBalances",
                        principalColumn: "BalanceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequestBalanceAllocations_LeaveRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "LeaveRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "LeaveTypes",
                columns: new[] { "LeaveTypeId", "AnnualQuota", "CarryOverRule", "EntitlementKind", "MaxAccrualDays", "Name" },
                values: new object[,]
                {
                    { 1, 30m, true, 1, 50m, "0-10 Yıllık Çalışan İzni" },
                    { 2, 30m, true, 2, 50m, "10-20 Yıllık Çalışan İzni" },
                    { 3, 30m, true, 3, 50m, "20-30 Yıllık Çalışan İzni" },
                    { 4, 30m, false, 4, 50m, "Hastalık İzni" },
                    { 5, 30m, false, 5, 50m, "Hamilelik İzni" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveTypes_HalfDayAmounts",
                table: "LeaveTypes",
                sql: "[AnnualQuota] >= 0 AND [AnnualQuota] * 2 = FLOOR([AnnualQuota] * 2) AND [MaxAccrualDays] > 0 AND [MaxAccrualDays] * 2 = FLOOR([MaxAccrualDays] * 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveTypes_PositiveId",
                table: "LeaveTypes",
                sql: "[LeaveTypeId] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveRequests_Category",
                table: "LeaveRequests",
                sql: "[Category] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveRequests_HalfDayAmount",
                table: "LeaveRequests",
                sql: "[RequestedDays] > 0 AND [RequestedDays] * 2 = FLOOR([RequestedDays] * 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveBalances_HalfDayAmounts",
                table: "LeaveBalances",
                sql: "[EntitledDays] >= 0 AND [EntitledDays] * 2 = FLOOR([EntitledDays] * 2) AND [CarryOverDays] >= 0 AND [CarryOverDays] * 2 = FLOOR([CarryOverDays] * 2) AND [UsedDays] >= 0 AND [UsedDays] * 2 = FLOOR([UsedDays] * 2) AND [RemainingDays] >= 0 AND [RemainingDays] * 2 = FLOOR([RemainingDays] * 2) AND [RemainingDays] = [EntitledDays] + [CarryOverDays] - [UsedDays]");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveBalances_LeaveTypes_LeaveTypeId",
                table: "LeaveBalances",
                column: "LeaveTypeId",
                principalTable: "LeaveTypes",
                principalColumn: "LeaveTypeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                "DBCC CHECKIDENT ('LeaveTypes', RESEED, 5) WITH NO_INFOMSGS;");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCarryOverWarnings_BalanceId",
                table: "LeaveCarryOverWarnings",
                column: "BalanceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCarryOverWarnings_EmployeeId",
                table: "LeaveCarryOverWarnings",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCarryOverWarnings_IsAcknowledged_Year",
                table: "LeaveCarryOverWarnings",
                columns: new[] { "IsAcknowledged", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCarryOverWarnings_LeaveTypeId",
                table: "LeaveCarryOverWarnings",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestBalanceAllocations_BalanceId",
                table: "LeaveRequestBalanceAllocations",
                column: "BalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestBalanceAllocations_RequestId_BalanceId_Source",
                table: "LeaveRequestBalanceAllocations",
                columns: new[] { "RequestId", "BalanceId", "Source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Bu DEVELOPMENT temiz geçişi eski izin verisini sıfırlar ve geriye dönük dönüştürülemez. "
                + "Geri dönüş için geçiş öncesi veritabanı yedeğini geri yükleyin.");
        }
    }
}
