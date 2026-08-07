using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class UseEmployeeAuditActors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActorEmployeeId",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemActorKey",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [AuditLogs]
                    WHERE LOWER(LTRIM(RTRIM([UserId]))) NOT IN
                        ('admin', 'user', 'hr', 'system', 'daily-leave-entitlement-worker')
                )
                    THROW 51000, 'AuditLogs.UserId contains an unmapped actor; employee audit migration stopped.', 1;

                UPDATE [AuditLogs]
                SET [ActorEmployeeId] = CASE LOWER(LTRIM(RTRIM([UserId])))
                        WHEN 'admin' THEN 1
                        WHEN 'user' THEN 2
                        WHEN 'hr' THEN 1002
                    END,
                    [SystemActorKey] = CASE LOWER(LTRIM(RTRIM([UserId])))
                        WHEN 'system' THEN 'System'
                        WHEN 'daily-leave-entitlement-worker' THEN 'daily-leave-entitlement-worker'
                    END;

                IF EXISTS (
                    SELECT 1
                    FROM [AuditLogs] AS [audit]
                    WHERE [audit].[ActorEmployeeId] IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1
                          FROM [Employees] AS [employee]
                          WHERE [employee].[EmployeeId] = [audit].[ActorEmployeeId]
                      )
                )
                    THROW 51001, 'An audit actor EmployeeId does not exist; employee audit migration stopped.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [AuditLogs]
                    WHERE ([ActorEmployeeId] IS NULL AND [SystemActorKey] IS NULL)
                       OR ([ActorEmployeeId] IS NOT NULL AND [SystemActorKey] IS NOT NULL)
                )
                    THROW 51002, 'Audit actor backfill is incomplete; employee audit migration stopped.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorEmployeeId",
                table: "AuditLogs",
                column: "ActorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SystemActorKey",
                table: "AuditLogs",
                column: "SystemActorKey");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Actor",
                table: "AuditLogs",
                sql: "([ActorEmployeeId] IS NOT NULL AND [SystemActorKey] IS NULL) OR ([ActorEmployeeId] IS NULL AND [SystemActorKey] IS NOT NULL AND [SystemActorKey] <> '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActorEmployeeId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_SystemActorKey",
                table: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Actor",
                table: "AuditLogs");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [AuditLogs]
                    WHERE ([ActorEmployeeId] IS NOT NULL AND [ActorEmployeeId] NOT IN (1, 2, 1002))
                       OR ([SystemActorKey] IS NOT NULL AND [SystemActorKey] NOT IN
                           ('System', 'daily-leave-entitlement-worker'))
                )
                    THROW 51003, 'Audit actors cannot be represented by the legacy username model; rollback stopped.', 1;

                UPDATE [AuditLogs]
                SET [UserId] = CASE [ActorEmployeeId]
                        WHEN 1 THEN 'admin'
                        WHEN 2 THEN 'user'
                        WHEN 1002 THEN 'hr'
                    END;

                UPDATE [AuditLogs]
                SET [UserId] = [SystemActorKey]
                WHERE [SystemActorKey] IS NOT NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "ActorEmployeeId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "SystemActorKey",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");
        }
    }
}
