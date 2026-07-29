using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveDepartmentDelegateAndLeaveTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [ManagerDelegations]
                    WHERE [IsActive] = 1
                )
                BEGIN
                    THROW 51005, 'Aktif vekalet kaydi varken ActiveDelegateEmployeeId kesintisiz bicimde turetilemez. Gelistirme verisini temizleyip migrationi yeniden calistirin.', 1;
                END
                """);

            migrationBuilder.DropIndex(
                name: "IX_ManagerDelegations_LeaveRequestId",
                table: "ManagerDelegations");

            migrationBuilder.DropIndex(
                name: "UX_ManagerDelegations_Department_Active",
                table: "ManagerDelegations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ManagerDelegations");

            migrationBuilder.AlterColumn<int>(
                name: "LeaveRequestId",
                table: "ManagerDelegations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<long>(
                name: "ParentManagerDelegationId",
                table: "ManagerDelegations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActiveDelegateEmployeeId",
                table: "Departments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagerDelegations_DepartmentId_RestoredAt",
                table: "ManagerDelegations",
                columns: new[] { "DepartmentId", "RestoredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ManagerDelegations_ParentManagerDelegationId",
                table: "ManagerDelegations",
                column: "ParentManagerDelegationId");

            migrationBuilder.CreateIndex(
                name: "UX_ManagerDelegations_LeaveRequest",
                table: "ManagerDelegations",
                column: "LeaveRequestId",
                unique: true,
                filter: "[LeaveRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_ActiveDelegateEmployeeId",
                table: "Departments",
                column: "ActiveDelegateEmployeeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs",
                sql: "[ActionType] BETWEEN 1 AND 32");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Employees_ActiveDelegateEmployeeId",
                table: "Departments",
                column: "ActiveDelegateEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ManagerDelegations_ManagerDelegations_ParentManagerDelegationId",
                table: "ManagerDelegations",
                column: "ParentManagerDelegationId",
                principalTable: "ManagerDelegations",
                principalColumn: "ManagerDelegationId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Employees_ActiveDelegateEmployeeId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_ManagerDelegations_ManagerDelegations_ParentManagerDelegationId",
                table: "ManagerDelegations");

            migrationBuilder.DropIndex(
                name: "IX_ManagerDelegations_DepartmentId_RestoredAt",
                table: "ManagerDelegations");

            migrationBuilder.DropIndex(
                name: "IX_ManagerDelegations_ParentManagerDelegationId",
                table: "ManagerDelegations");

            migrationBuilder.DropIndex(
                name: "UX_ManagerDelegations_LeaveRequest",
                table: "ManagerDelegations");

            migrationBuilder.DropIndex(
                name: "IX_Departments_ActiveDelegateEmployeeId",
                table: "Departments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ParentManagerDelegationId",
                table: "ManagerDelegations");

            migrationBuilder.DropColumn(
                name: "ActiveDelegateEmployeeId",
                table: "Departments");

            migrationBuilder.AlterColumn<int>(
                name: "LeaveRequestId",
                table: "ManagerDelegations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ManagerDelegations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ManagerDelegations_LeaveRequestId",
                table: "ManagerDelegations",
                column: "LeaveRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ManagerDelegations_Department_Active",
                table: "ManagerDelegations",
                columns: new[] { "DepartmentId", "IsActive" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs",
                sql: "[ActionType] BETWEEN 1 AND 31");
        }
    }
}
