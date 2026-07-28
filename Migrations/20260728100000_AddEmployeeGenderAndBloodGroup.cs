using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations;

[DbContext(typeof(Database.HumanResourcesDbContext))]
[Migration("20260728100000_AddEmployeeGenderAndBloodGroup")]
public partial class AddEmployeeGenderAndBloodGroup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Gender",
            table: "Employees",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "BloodGroup",
            table: "Employees",
            type: "int",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Employees_Gender",
            table: "Employees",
            sql: "[Gender] IS NULL OR [Gender] IN (1, 2)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Employees_BloodGroup",
            table: "Employees",
            sql: "[BloodGroup] IS NULL OR [BloodGroup] BETWEEN 1 AND 8");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_Employees_BloodGroup",
            table: "Employees");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Employees_Gender",
            table: "Employees");

        migrationBuilder.DropColumn(
            name: "BloodGroup",
            table: "Employees");

        migrationBuilder.DropColumn(
            name: "Gender",
            table: "Employees");
    }
}
