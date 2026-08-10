using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class SynchronizeManagerResponsibilityRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @PromotedEmployees TABLE
                (
                    [EmployeeId] int NOT NULL PRIMARY KEY,
                    [PreviousRoleId] int NOT NULL
                );

                UPDATE employee
                SET employee.[ApplicationRoleId] = 2
                OUTPUT inserted.[EmployeeId], deleted.[ApplicationRoleId]
                    INTO @PromotedEmployees ([EmployeeId], [PreviousRoleId])
                FROM [Employees] AS employee
                WHERE employee.[ApplicationRoleId] = 1
                  AND EXISTS
                  (
                      SELECT 1
                      FROM [Departments] AS department
                      WHERE department.[ManagerEmployeeId] = employee.[EmployeeId]
                         OR department.[ActiveDelegateEmployeeId] = employee.[EmployeeId]
                  );

                INSERT INTO [AuditLogs]
                    ([ActorEmployeeId], [SystemActorKey], [ActionType], [EntityName], [EntityId], [ActionDate], [Details])
                SELECT
                    NULL,
                    N'manager-role-backfill',
                    3,
                    N'Employee',
                    CONVERT(nvarchar(64), promoted.[EmployeeId]),
                    SYSUTCDATETIME(),
                    CONCAT(
                        N'ApplicationRoleId=', promoted.[PreviousRoleId],
                        N'->2; Source=ManagerResponsibilityBackfill; HasManagementResponsibility=True')
                FROM @PromotedEmployees AS promoted;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                THROW 51070, 'Manager responsibility role synchronization cannot be rolled back without restoring the pre-migration database backup.', 1;
                """);
        }
    }
}
