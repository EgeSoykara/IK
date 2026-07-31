using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticLeaveEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntitlementKind",
                table: "LeaveTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveTypes_EntitlementKind",
                table: "LeaveTypes",
                sql: "[EntitlementKind] BETWEEN 0 AND 5");

            migrationBuilder.InsertData(
                table: "LeaveTypes",
                columns: new[] { "LeaveTypeId", "AnnualQuota", "CarryOverRule", "EntitlementKind", "MaxAccrualDays", "Name" },
                values: new object[,]
                {
                    { -5, 30m, false, 5, 50m, "Hamilelik İzni" },
                    { -4, 30m, false, 4, 50m, "Hastalık İzni" },
                    { -3, 30m, true, 3, 50m, "20-30 Yıllık Çalışan İzni" },
                    { -2, 30m, true, 2, 50m, "10-20 Yıllık Çalışan İzni" },
                    { -1, 30m, true, 1, 50m, "0-10 Yıllık Çalışan İzni" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "LeaveTypes",
                keyColumn: "LeaveTypeId",
                keyValue: -5);

            migrationBuilder.DeleteData(
                table: "LeaveTypes",
                keyColumn: "LeaveTypeId",
                keyValue: -4);

            migrationBuilder.DeleteData(
                table: "LeaveTypes",
                keyColumn: "LeaveTypeId",
                keyValue: -3);

            migrationBuilder.DeleteData(
                table: "LeaveTypes",
                keyColumn: "LeaveTypeId",
                keyValue: -2);

            migrationBuilder.DeleteData(
                table: "LeaveTypes",
                keyColumn: "LeaveTypeId",
                keyValue: -1);

            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveTypes_EntitlementKind",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "EntitlementKind",
                table: "LeaveTypes");
        }
    }
}
