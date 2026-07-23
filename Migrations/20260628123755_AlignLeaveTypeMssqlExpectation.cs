using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class AlignLeaveTypeMssqlExpectation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MaxAccrualWarningDays",
                table: "LeaveTypes",
                newName: "MaxAccrualDays");

            migrationBuilder.RenameColumn(
                name: "AnnualQuotaDays",
                table: "LeaveTypes",
                newName: "AnnualQuota");

            migrationBuilder.AddColumn<bool>(
                name: "CarryOverRule",
                table: "LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("UPDATE LeaveTypes SET CarryOverRule = IsActive;");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "LeaveTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("UPDATE LeaveTypes SET IsActive = CarryOverRule;");

            migrationBuilder.DropColumn(
                name: "CarryOverRule",
                table: "LeaveTypes");

            migrationBuilder.RenameColumn(
                name: "MaxAccrualDays",
                table: "LeaveTypes",
                newName: "MaxAccrualWarningDays");

            migrationBuilder.RenameColumn(
                name: "AnnualQuota",
                table: "LeaveTypes",
                newName: "AnnualQuotaDays");
        }
    }
}
