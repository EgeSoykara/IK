using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class ScopePersonnelInformationAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 2, "CanCreateNewEmployee" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 2, "CanEditDeleteLeaveRequests" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 2, "CanManageDepartments" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 2, "CanManageLeaveBalances" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 2, "CanManageLeaveTypes" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 2, "CanManagePublicHolidays" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 2, "CanViewAuditLogs" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 2, "CanviewEmployeeSearch" });

            migrationBuilder.InsertData(
                table: "ApplicationRolePermissions",
                columns: new[] { "ApplicationRoleId", "PermissionName" },
                values: new object[,]
                {
                    { 3, "CanAccessSensitivePersonnelInformation" },
                    { 3, "CanDownloadPersonnelDocuments" },
                    { 3, "CanEditAllPersonnelInformation" },
                    { 3, "CanViewAllPersonnelInformation" }
                });

            migrationBuilder.UpdateData(
                table: "ApplicationRoles",
                keyColumn: "ApplicationRoleId",
                keyValue: 2,
                column: "Description",
                value: "Departman yöneticisi erişimi");

            migrationBuilder.InsertData(
                table: "ApplicationRoles",
                columns: new[] { "ApplicationRoleId", "Description", "Name" },
                values: new object[] { 4, "Tam uygulama ve personel yönetimi", "Sistem Yöneticisi" });

            migrationBuilder.Sql(
                "UPDATE [Employees] SET [ApplicationRoleId] = 4 "
                + "WHERE [EmployeeId] = 1 AND [ApplicationRoleId] = 2;");

            migrationBuilder.InsertData(
                table: "ApplicationRolePermissions",
                columns: new[] { "ApplicationRoleId", "PermissionName" },
                values: new object[,]
                {
                    { 4, "CanAccessSensitivePersonnelInformation" },
                    { 4, "CanCreateNewEmployee" },
                    { 4, "CanDownloadPersonnelDocuments" },
                    { 4, "CanEditAllPersonnelInformation" },
                    { 4, "CanEditDeleteLeaveRequests" },
                    { 4, "CanExectuteApproveLeave" },
                    { 4, "CanManageDepartments" },
                    { 4, "CanManageLeaveBalances" },
                    { 4, "CanManageLeaveRequests" },
                    { 4, "CanManageLeaveTypes" },
                    { 4, "CanManagePublicHolidays" },
                    { 4, "CanViewAllPersonnelInformation" },
                    { 4, "CanViewAuditLogs" },
                    { 4, "CanviewEmployeeSearch" },
                    { 4, "CanViewLeaveRequests" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [Employees] SET [ApplicationRoleId] = 2 "
                + "WHERE [EmployeeId] = 1 AND [ApplicationRoleId] = 4;");

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 3, "CanAccessSensitivePersonnelInformation" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 3, "CanDownloadPersonnelDocuments" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 3, "CanEditAllPersonnelInformation" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 3, "CanViewAllPersonnelInformation" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanAccessSensitivePersonnelInformation" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanCreateNewEmployee" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanDownloadPersonnelDocuments" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanEditAllPersonnelInformation" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanEditDeleteLeaveRequests" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanExectuteApproveLeave" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanManageDepartments" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanManageLeaveBalances" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanManageLeaveRequests" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanManageLeaveTypes" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanManagePublicHolidays" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanViewAllPersonnelInformation" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanViewAuditLogs" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanviewEmployeeSearch" });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermissions",
                keyColumns: new[] { "ApplicationRoleId", "PermissionName" },
                keyValues: new object[] { 4, "CanViewLeaveRequests" });

            migrationBuilder.DeleteData(
                table: "ApplicationRoles",
                keyColumn: "ApplicationRoleId",
                keyValue: 4);

            migrationBuilder.InsertData(
                table: "ApplicationRolePermissions",
                columns: new[] { "ApplicationRoleId", "PermissionName" },
                values: new object[,]
                {
                    { 2, "CanCreateNewEmployee" },
                    { 2, "CanEditDeleteLeaveRequests" },
                    { 2, "CanManageDepartments" },
                    { 2, "CanManageLeaveBalances" },
                    { 2, "CanManageLeaveTypes" },
                    { 2, "CanManagePublicHolidays" },
                    { 2, "CanViewAuditLogs" },
                    { 2, "CanviewEmployeeSearch" }
                });

            migrationBuilder.UpdateData(
                table: "ApplicationRoles",
                keyColumn: "ApplicationRoleId",
                keyValue: 2,
                column: "Description",
                value: "Tam uygulama yönetimi");
        }
    }
}
