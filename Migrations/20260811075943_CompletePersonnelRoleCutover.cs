using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class CompletePersonnelRoleCutover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [employee] SET [ApplicationRoleId] = 4 "
                + "FROM [Employees] AS [employee] "
                + "WHERE [employee].[ApplicationRoleId] = 2 "
                + "AND NOT EXISTS ("
                + "SELECT 1 FROM [Departments] AS [department] "
                + "WHERE [department].[ManagerEmployeeId] = [employee].[EmployeeId] "
                + "OR [department].[ActiveDelegateEmployeeId] = [employee].[EmployeeId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [Employees] SET [ApplicationRoleId] = 2 "
                + "WHERE [ApplicationRoleId] = 4;");
        }
    }
}
