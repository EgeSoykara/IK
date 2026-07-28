using IK.Web.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations;

[DbContext(typeof(HumanResourcesDbContext))]
[Migration("20260728093000_AddEmployeeEducationLevel")]
public sealed class AddEmployeeEducationLevel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EducationLevel",
            table: "EmployeeEducations",
            type: "nvarchar(80)",
            maxLength: 80,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EducationLevel",
            table: "EmployeeEducations");
    }
}
