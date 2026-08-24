using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class UseActiveDirectoryAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeCredentials");

            migrationBuilder.DropIndex(
                name: "IX_Employees_KKTC_KimlikNo",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_SicilNo",
                table: "Employees");

            migrationBuilder.AlterColumn<string>(
                name: "SicilNo",
                table: "Employees",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "KKTC_KimlikNo",
                table: "Employees",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<int>(
                name: "DepartmentId",
                table: "Employees",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "SamAccountName",
                table: "Employees",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Employees_KktcKimlikNo",
                table: "Employees",
                column: "KKTC_KimlikNo",
                unique: true,
                filter: "[KKTC_KimlikNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Employees_SamAccountName",
                table: "Employees",
                column: "SamAccountName",
                unique: true,
                filter: "[SamAccountName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Employees_SicilNo",
                table: "Employees",
                column: "SicilNo",
                unique: true,
                filter: "[SicilNo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                THROW 51007, 'Active Directory authentication cutover cannot be rolled back because local password hashes and deferred personnel fields cannot be reconstructed.', 1;
                """);
        }
    }
}
