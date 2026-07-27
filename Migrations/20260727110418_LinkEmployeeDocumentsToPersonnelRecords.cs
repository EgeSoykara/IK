using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class LinkEmployeeDocumentsToPersonnelRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmployeeCourseCertificateId",
                table: "EmployeeDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeEducationId",
                table: "EmployeeDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeIdentityDocumentId",
                table: "EmployeeDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE document
                SET EmployeeIdentityDocumentId = candidate.EmployeeIdentityDocumentId
                FROM EmployeeDocuments AS document
                INNER JOIN
                (
                    SELECT EmployeeId, MIN(EmployeeIdentityDocumentId) AS EmployeeIdentityDocumentId
                    FROM EmployeeIdentityDocuments
                    GROUP BY EmployeeId
                    HAVING COUNT(*) = 1
                ) AS candidate ON candidate.EmployeeId = document.EmployeeId
                WHERE document.CategoryCanonicalKey = N'identity'
                  AND document.EmployeeIdentityDocumentId IS NULL;

                UPDATE document
                SET EmployeeEducationId = candidate.EmployeeEducationId
                FROM EmployeeDocuments AS document
                INNER JOIN
                (
                    SELECT EmployeeId, MIN(EmployeeEducationId) AS EmployeeEducationId
                    FROM EmployeeEducations
                    GROUP BY EmployeeId
                    HAVING COUNT(*) = 1
                ) AS candidate ON candidate.EmployeeId = document.EmployeeId
                WHERE document.CategoryCanonicalKey = N'education'
                  AND document.EmployeeEducationId IS NULL
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM EmployeeCourseCertificates AS course
                      WHERE course.EmployeeId = document.EmployeeId
                  );

                UPDATE document
                SET EmployeeCourseCertificateId = candidate.EmployeeCourseCertificateId
                FROM EmployeeDocuments AS document
                INNER JOIN
                (
                    SELECT EmployeeId, MIN(EmployeeCourseCertificateId) AS EmployeeCourseCertificateId
                    FROM EmployeeCourseCertificates
                    GROUP BY EmployeeId
                    HAVING COUNT(*) = 1
                ) AS candidate ON candidate.EmployeeId = document.EmployeeId
                WHERE document.CategoryCanonicalKey = N'education'
                  AND document.EmployeeCourseCertificateId IS NULL
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM EmployeeEducations AS education
                      WHERE education.EmployeeId = document.EmployeeId
                  );
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeDocuments_SingleRelatedRecord",
                table: "EmployeeDocuments",
                sql: "(CASE WHEN [EmployeeIdentityDocumentId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [EmployeeEducationId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [EmployeeCourseCertificateId] IS NULL THEN 0 ELSE 1 END) <= 1");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeCourseCertificateId",
                table: "EmployeeDocuments",
                column: "EmployeeCourseCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeEducationId",
                table: "EmployeeDocuments",
                column: "EmployeeEducationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeIdentityDocumentId",
                table: "EmployeeDocuments",
                column: "EmployeeIdentityDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDocuments_EmployeeCourseCertificates_EmployeeCourseCertificateId",
                table: "EmployeeDocuments",
                column: "EmployeeCourseCertificateId",
                principalTable: "EmployeeCourseCertificates",
                principalColumn: "EmployeeCourseCertificateId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDocuments_EmployeeEducations_EmployeeEducationId",
                table: "EmployeeDocuments",
                column: "EmployeeEducationId",
                principalTable: "EmployeeEducations",
                principalColumn: "EmployeeEducationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDocuments_EmployeeIdentityDocuments_EmployeeIdentityDocumentId",
                table: "EmployeeDocuments",
                column: "EmployeeIdentityDocumentId",
                principalTable: "EmployeeIdentityDocuments",
                principalColumn: "EmployeeIdentityDocumentId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeDocuments_SingleRelatedRecord",
                table: "EmployeeDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDocuments_EmployeeCourseCertificates_EmployeeCourseCertificateId",
                table: "EmployeeDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDocuments_EmployeeEducations_EmployeeEducationId",
                table: "EmployeeDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDocuments_EmployeeIdentityDocuments_EmployeeIdentityDocumentId",
                table: "EmployeeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeCourseCertificateId",
                table: "EmployeeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeEducationId",
                table: "EmployeeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeIdentityDocumentId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "EmployeeCourseCertificateId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "EmployeeEducationId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "EmployeeIdentityDocumentId",
                table: "EmployeeDocuments");
        }
    }
}
