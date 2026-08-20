using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeCredentialsAndEmailLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [Employees])
                    THROW 51006, 'Email credential cutover requires an empty DEVELOPMENT Employees table. Reset the database and rerun migrations.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "UX_Employees_Email",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Employees",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeCredentials",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PasswordChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCredentials", x => x.EmployeeId);
                    table.ForeignKey(
                        name: "FK_EmployeeCredentials_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Employees_Email",
                table: "Employees",
                column: "Email",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs",
                sql: "[ActionType] BETWEEN 1 AND 44");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeCredentials");

            migrationBuilder.DropIndex(
                name: "UX_Employees_Email",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Employees",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(254)",
                oldMaxLength: 254);

            migrationBuilder.CreateIndex(
                name: "UX_Employees_Email",
                table: "Employees",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_ActionType",
                table: "AuditLogs",
                sql: "[ActionType] BETWEEN 1 AND 43");
        }
    }
}
