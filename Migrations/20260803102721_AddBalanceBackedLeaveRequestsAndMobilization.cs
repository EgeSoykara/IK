using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddBalanceBackedLeaveRequestsAndMobilization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveTypes_EntitlementKind",
                table: "LeaveTypes");

            // DEVELOPMENT clean cutover: the superseded fixed-category request model and
            // any manual type occupying identity 6 are reset instead of converted. Employee
            // and department records remain intact; all derived leave-domain rows and their
            // audit history are removed before the single 1-6 seed authority is rebuilt.
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM Departments
                    WHERE ActiveDelegateEmployeeId IS NOT NULL
                      AND ManagerEmployeeId IS NULL
                )
                BEGIN
                    THROW 51007, 'Aktif vekaletin asil departman yoneticisi bulunamadigi icin DEVELOPMENT cutover guvenle tamamlanamadi.', 1;
                END;

                UPDATE employee
                SET ManagerId = CASE
                    WHEN employee.EmployeeId = department.ManagerEmployeeId
                        THEN parentDepartment.ManagerEmployeeId
                    ELSE department.ManagerEmployeeId
                END
                FROM Employees AS employee
                INNER JOIN Departments AS department
                    ON department.DepartmentId = employee.DepartmentId
                LEFT JOIN Departments AS parentDepartment
                    ON parentDepartment.DepartmentId = department.ParentDepartmentId
                WHERE department.ActiveDelegateEmployeeId IS NOT NULL;

                UPDATE childManager
                SET ManagerId = parentDepartment.ManagerEmployeeId
                FROM Departments AS childDepartment
                INNER JOIN Employees AS childManager
                    ON childManager.EmployeeId = childDepartment.ManagerEmployeeId
                INNER JOIN Departments AS parentDepartment
                    ON parentDepartment.DepartmentId = childDepartment.ParentDepartmentId
                WHERE parentDepartment.ActiveDelegateEmployeeId IS NOT NULL;

                UPDATE Departments
                SET ActiveDelegateEmployeeId = NULL
                WHERE ActiveDelegateEmployeeId IS NOT NULL;

                DELETE FROM ManagerDelegations;
                DELETE FROM LeaveApprovals;
                DELETE FROM LeaveRequestBalanceAllocations;
                DELETE FROM LeaveRequests;
                DELETE FROM LeaveCarryOverWarnings;
                DELETE FROM LeaveBalances;
                DELETE FROM AuditLogs
                WHERE EntityName IN ('LeaveType', 'LeaveBalance', 'LeaveRequest', 'ManagerDelegation');
                DELETE FROM LeaveTypes;

                DBCC CHECKIDENT ('ManagerDelegations', RESEED, 0) WITH NO_INFOMSGS;
                DBCC CHECKIDENT ('LeaveApprovals', RESEED, 0) WITH NO_INFOMSGS;
                DBCC CHECKIDENT ('LeaveRequestBalanceAllocations', RESEED, 0) WITH NO_INFOMSGS;
                DBCC CHECKIDENT ('LeaveRequests', RESEED, 0) WITH NO_INFOMSGS;
                DBCC CHECKIDENT ('LeaveCarryOverWarnings', RESEED, 0) WITH NO_INFOMSGS;
                DBCC CHECKIDENT ('LeaveBalances', RESEED, 0) WITH NO_INFOMSGS;
                DBCC CHECKIDENT ('LeaveTypes', RESEED, 0) WITH NO_INFOMSGS;
                """);

            migrationBuilder.AddColumn<int>(
                name: "LeaveTypeId",
                table: "LeaveRequests",
                type: "int",
                nullable: true);

            migrationBuilder.InsertData(
                table: "LeaveTypes",
                columns: new[] { "LeaveTypeId", "AnnualQuota", "CarryOverRule", "EntitlementKind", "MaxAccrualDays", "Name" },
                values: new object[,]
                {
                    { 1, 30m, true, 1, 50m, "0-10 Yıllık Çalışan İzni" },
                    { 2, 30m, true, 2, 50m, "10-20 Yıllık Çalışan İzni" },
                    { 3, 30m, true, 3, 50m, "20-30 Yıllık Çalışan İzni" },
                    { 4, 30m, false, 4, 50m, "Hastalık İzni" },
                    { 5, 30m, false, 5, 50m, "Hamilelik İzni" },
                    { 6, 2m, false, 6, 2m, "Seferberlik İzni" }
                });

            migrationBuilder.Sql(
                "DBCC CHECKIDENT ('LeaveTypes', RESEED, 6) WITH NO_INFOMSGS;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveTypes_EntitlementKind",
                table: "LeaveTypes",
                sql: "[EntitlementKind] BETWEEN 0 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveTypes_MobilizationPolicy",
                table: "LeaveTypes",
                sql: "[EntitlementKind] <> 6 OR ([AnnualQuota] <= 2 AND [CarryOverRule] = 0 AND [MaxAccrualDays] <= 2)");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_LeaveTypeId",
                table: "LeaveRequests",
                column: "LeaveTypeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveRequests_LeaveTypeSelection",
                table: "LeaveRequests",
                sql: "([Category] = 1 AND [LeaveTypeId] IS NULL) OR ([Category] = 2 AND [LeaveTypeId] IS NOT NULL AND [LeaveTypeId] > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveBalances_MobilizationMaximum",
                table: "LeaveBalances",
                sql: "[LeaveTypeId] <> 6 OR [EntitledDays] + [CarryOverDays] <= 2");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_LeaveTypes_LeaveTypeId",
                table: "LeaveRequests",
                column: "LeaveTypeId",
                principalTable: "LeaveTypes",
                principalColumn: "LeaveTypeId",
                onDelete: ReferentialAction.Restrict);
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
