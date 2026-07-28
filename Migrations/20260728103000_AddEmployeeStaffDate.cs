using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations;

[DbContext(typeof(Database.HumanResourcesDbContext))]
[Migration("20260728103000_AddEmployeeStaffDate")]
public partial class AddEmployeeStaffDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "StaffDate",
            table: "Employees",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "StaffDate",
            table: "Employees");
    }
}
