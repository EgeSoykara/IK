using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentManagerDelegation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs");

            migrationBuilder.AddColumn<int>(
                name: "DelegateEmployeeId",
                table: "LeaveRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManagerEmployeeId",
                table: "Departments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ManagerDelegations",
                columns: table => new
                {
                    ManagerDelegationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaveRequestId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    ManagerEmployeeId = table.Column<int>(type: "int", nullable: false),
                    DelegateEmployeeId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RestoredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerDelegations", x => x.ManagerDelegationId);
                    table.CheckConstraint("CK_ManagerDelegations_DateRange", "[EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_ManagerDelegations_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManagerDelegations_Employees_DelegateEmployeeId",
                        column: x => x.DelegateEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManagerDelegations_Employees_ManagerEmployeeId",
                        column: x => x.ManagerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManagerDelegations_LeaveRequests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalTable: "LeaveRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_DelegateEmployeeId",
                table: "LeaveRequests",
                column: "DelegateEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_ManagerEmployeeId",
                table: "Departments",
                column: "ManagerEmployeeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs",
                sql: "[ActionType] BETWEEN 1 AND 31");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerDelegations_DelegateEmployeeId",
                table: "ManagerDelegations",
                column: "DelegateEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerDelegations_LeaveRequestId",
                table: "ManagerDelegations",
                column: "LeaveRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagerDelegations_ManagerEmployeeId",
                table: "ManagerDelegations",
                column: "ManagerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "UX_ManagerDelegations_Department_Active",
                table: "ManagerDelegations",
                columns: new[] { "DepartmentId", "IsActive" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Employees_ManagerEmployeeId",
                table: "Departments",
                column: "ManagerEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_Employees_DelegateEmployeeId",
                table: "LeaveRequests",
                column: "DelegateEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Employees_ManagerEmployeeId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_Employees_DelegateEmployeeId",
                table: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "ManagerDelegations");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_DelegateEmployeeId",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_Departments_ManagerEmployeeId",
                table: "Departments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "DelegateEmployeeId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "ManagerEmployeeId",
                table: "Departments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs",
                sql: "[ActionType] BETWEEN 1 AND 27");
        }
    }
}
