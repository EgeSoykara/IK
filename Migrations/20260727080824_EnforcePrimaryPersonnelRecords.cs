using IK.Web.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations;

/// <inheritdoc />
[DbContext(typeof(HumanResourcesDbContext))]
[Migration("20260727080824_EnforcePrimaryPersonnelRecords")]
public partial class EnforcePrimaryPersonnelRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "UX_EmployeeBankAccounts_EmployeeId_Primary",
            table: "EmployeeBankAccounts",
            columns: ["EmployeeId", "IsPrimary"],
            unique: true,
            filter: "[IsPrimary] = 1");

        migrationBuilder.CreateIndex(
            name: "UX_EmployeePhones_EmployeeId_Primary",
            table: "EmployeePhones",
            columns: ["EmployeeId", "IsPrimary"],
            unique: true,
            filter: "[IsPrimary] = 1");

        migrationBuilder.CreateIndex(
            name: "UX_EmployeeAddresses_EmployeeId_Primary",
            table: "EmployeeAddresses",
            columns: ["EmployeeId", "IsPrimary"],
            unique: true,
            filter: "[IsPrimary] = 1");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_EmployeeBankAccounts_EmployeeId_Primary",
            table: "EmployeeBankAccounts");

        migrationBuilder.DropIndex(
            name: "UX_EmployeePhones_EmployeeId_Primary",
            table: "EmployeePhones");

        migrationBuilder.DropIndex(
            name: "UX_EmployeeAddresses_EmployeeId_Primary",
            table: "EmployeeAddresses");
    }
}
