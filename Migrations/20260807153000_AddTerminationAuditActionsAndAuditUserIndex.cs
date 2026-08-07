using IK.Web.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations;

[DbContext(typeof(HumanResourcesDbContext))]
[Migration("20260807153000_AddTerminationAuditActionsAndAuditUserIndex")]
public sealed class AddTerminationAuditActionsAndAuditUserIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_AuditLogs_ActionType",
            table: "AuditLogs");

        migrationBuilder.AddCheckConstraint(
            name: "CK_AuditLogs_ActionType",
            table: "AuditLogs",
            sql: "[ActionType] BETWEEN 1 AND 41");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_UserId",
            table: "AuditLogs",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AuditLogs_UserId",
            table: "AuditLogs");

        migrationBuilder.DropCheckConstraint(
            name: "CK_AuditLogs_ActionType",
            table: "AuditLogs");

        migrationBuilder.AddCheckConstraint(
            name: "CK_AuditLogs_ActionType",
            table: "AuditLogs",
            sql: "[ActionType] BETWEEN 1 AND 38");
    }
}
