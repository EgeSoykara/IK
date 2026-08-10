using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class PersistApplicationRolesAndEmployeeEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplicationRoleId",
                table: "Employees",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Employees",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationRoles",
                columns: table => new
                {
                    ApplicationRoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationRoles", x => x.ApplicationRoleId);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationRolePermissions",
                columns: table => new
                {
                    ApplicationRoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationRolePermissions", x => new { x.ApplicationRoleId, x.PermissionName });
                    table.ForeignKey(
                        name: "FK_ApplicationRolePermissions_ApplicationRoles_ApplicationRoleId",
                        column: x => x.ApplicationRoleId,
                        principalTable: "ApplicationRoles",
                        principalColumn: "ApplicationRoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ApplicationRoles",
                columns: new[] { "ApplicationRoleId", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Standart çalışan erişimi", "Çalışan" },
                    { 2, "Tam uygulama yönetimi", "Yönetici" },
                    { 3, "İnsan kaynakları yönetimi", "İnsan Kaynakları" }
                });

            migrationBuilder.InsertData(
                table: "ApplicationRolePermissions",
                columns: new[] { "ApplicationRoleId", "PermissionName" },
                values: new object[,]
                {
                    { 1, "CanManageLeaveRequests" },
                    { 2, "CanCreateNewEmployee" },
                    { 2, "CanEditDeleteLeaveRequests" },
                    { 2, "CanExectuteApproveLeave" },
                    { 2, "CanManageDepartments" },
                    { 2, "CanManageLeaveBalances" },
                    { 2, "CanManageLeaveRequests" },
                    { 2, "CanManageLeaveTypes" },
                    { 2, "CanManagePublicHolidays" },
                    { 2, "CanViewAuditLogs" },
                    { 2, "CanviewEmployeeSearch" },
                    { 2, "CanViewLeaveRequests" },
                    { 3, "CanActAsHumanResources" },
                    { 3, "CanCreateNewEmployee" },
                    { 3, "CanEditDeleteLeaveRequests" },
                    { 3, "CanExectuteApproveLeave" },
                    { 3, "CanManageDepartments" },
                    { 3, "CanManageLeaveBalances" },
                    { 3, "CanManageLeaveRequests" },
                    { 3, "CanManageLeaveTypes" },
                    { 3, "CanManagePublicHolidays" },
                    { 3, "CanViewAuditLogs" },
                    { 3, "CanviewEmployeeSearch" },
                    { 3, "CanViewLeaveRequests" }
                });

            migrationBuilder.Sql(
                """
                UPDATE [Employees]
                SET [ApplicationRoleId] = CASE
                    WHEN [EmployeeId] = 1 THEN 2
                    WHEN [EmployeeId] = 1002 THEN 3
                    ELSE 1
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "ApplicationRoleId",
                table: "Employees",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ApplicationRoleId",
                table: "Employees",
                column: "ApplicationRoleId");

            migrationBuilder.CreateIndex(
                name: "UX_Employees_Email",
                table: "Employees",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRolePermissions_PermissionName",
                table: "ApplicationRolePermissions",
                column: "PermissionName");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRoles_Name",
                table: "ApplicationRoles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_ApplicationRoles_ApplicationRoleId",
                table: "Employees",
                column: "ApplicationRoleId",
                principalTable: "ApplicationRoles",
                principalColumn: "ApplicationRoleId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_ApplicationRoles_ApplicationRoleId",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "ApplicationRolePermissions");

            migrationBuilder.DropTable(
                name: "ApplicationRoles");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ApplicationRoleId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "UX_Employees_Email",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ApplicationRoleId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Employees");
        }
    }
}
