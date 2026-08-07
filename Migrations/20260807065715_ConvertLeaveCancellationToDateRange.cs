using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class ConvertLeaveCancellationToDateRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ManagerDelegations_LeaveRequest",
                table: "ManagerDelegations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveCancellationRequests_DateRange",
                table: "LeaveCancellationRequests");

            migrationBuilder.RenameColumn(
                name: "ReturnDate",
                table: "LeaveCancellationRequests",
                newName: "CancellationStartDate");

            migrationBuilder.RenameColumn(
                name: "OriginalEndDate",
                table: "LeaveCancellationRequests",
                newName: "CancellationEndDate");

            migrationBuilder.AddColumn<bool>(
                name: "IsDirectCancellation",
                table: "LeaveCancellationRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "UX_ManagerDelegations_LeaveRequest",
                table: "ManagerDelegations",
                column: "LeaveRequestId",
                unique: true,
                filter: "[LeaveRequestId] IS NOT NULL AND [RestoredAt] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveCancellationRequests_DateRange",
                table: "LeaveCancellationRequests",
                sql: "[CancellationStartDate] <= [CancellationEndDate]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [LeaveCancellationRequests])
                BEGIN
                    THROW 50001, N'İptal aralığı veya doğrudan iptal geçmişi varken eski tek dönüş tarihi modeline kayıpsız dönülemez. Migration öncesi yedeği geri yükleyin.', 1;
                END

                IF EXISTS (
                    SELECT [LeaveRequestId]
                    FROM [ManagerDelegations]
                    WHERE [LeaveRequestId] IS NOT NULL
                    GROUP BY [LeaveRequestId]
                    HAVING COUNT(*) > 1)
                BEGIN
                    THROW 50002, N'Bir izin için birden fazla vekâlet geçmişi varken eski tek-kayıt indeksine kayıpsız dönülemez.', 1;
                END
                """);

            migrationBuilder.DropIndex(
                name: "UX_ManagerDelegations_LeaveRequest",
                table: "ManagerDelegations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveCancellationRequests_DateRange",
                table: "LeaveCancellationRequests");

            migrationBuilder.DropColumn(
                name: "IsDirectCancellation",
                table: "LeaveCancellationRequests");

            migrationBuilder.RenameColumn(
                name: "CancellationStartDate",
                table: "LeaveCancellationRequests",
                newName: "ReturnDate");

            migrationBuilder.RenameColumn(
                name: "CancellationEndDate",
                table: "LeaveCancellationRequests",
                newName: "OriginalEndDate");

            migrationBuilder.CreateIndex(
                name: "UX_ManagerDelegations_LeaveRequest",
                table: "ManagerDelegations",
                column: "LeaveRequestId",
                unique: true,
                filter: "[LeaveRequestId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveCancellationRequests_DateRange",
                table: "LeaveCancellationRequests",
                sql: "[ReturnDate] <= [OriginalEndDate]");
        }
    }
}
