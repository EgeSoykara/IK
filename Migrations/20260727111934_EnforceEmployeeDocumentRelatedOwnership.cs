using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class EnforceEmployeeDocumentRelatedOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS
                (
                    SELECT 1
                    FROM EmployeeDocuments AS document
                    LEFT JOIN EmployeeIdentityDocuments AS identityRecord
                        ON identityRecord.EmployeeId = document.EmployeeId
                        AND identityRecord.EmployeeIdentityDocumentId = document.EmployeeIdentityDocumentId
                    LEFT JOIN EmployeeEducations AS educationRecord
                        ON educationRecord.EmployeeId = document.EmployeeId
                        AND educationRecord.EmployeeEducationId = document.EmployeeEducationId
                    LEFT JOIN EmployeeCourseCertificates AS courseRecord
                        ON courseRecord.EmployeeId = document.EmployeeId
                        AND courseRecord.EmployeeCourseCertificateId = document.EmployeeCourseCertificateId
                    WHERE (document.EmployeeIdentityDocumentId IS NOT NULL
                            AND identityRecord.EmployeeIdentityDocumentId IS NULL)
                        OR (document.EmployeeEducationId IS NOT NULL
                            AND educationRecord.EmployeeEducationId IS NULL)
                        OR (document.EmployeeCourseCertificateId IS NOT NULL
                            AND courseRecord.EmployeeCourseCertificateId IS NULL)
                )
                BEGIN
                    THROW 51000, 'Employee document related-record ownership mismatch detected.', 1;
                END
                """);

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

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EmployeeIdentityDocuments_EmployeeId_RecordId",
                table: "EmployeeIdentityDocuments",
                columns: new[] { "EmployeeId", "EmployeeIdentityDocumentId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EmployeeEducations_EmployeeId_RecordId",
                table: "EmployeeEducations",
                columns: new[] { "EmployeeId", "EmployeeEducationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EmployeeCourseCertificates_EmployeeId_RecordId",
                table: "EmployeeCourseCertificates",
                columns: new[] { "EmployeeId", "EmployeeCourseCertificateId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_EmployeeCourseCertificateId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "EmployeeCourseCertificateId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_EmployeeEducationId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "EmployeeEducationId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_EmployeeIdentityDocumentId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "EmployeeIdentityDocumentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDocuments_EmployeeCourseCertificates_EmployeeId_EmployeeCourseCertificateId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "EmployeeCourseCertificateId" },
                principalTable: "EmployeeCourseCertificates",
                principalColumns: new[] { "EmployeeId", "EmployeeCourseCertificateId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDocuments_EmployeeEducations_EmployeeId_EmployeeEducationId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "EmployeeEducationId" },
                principalTable: "EmployeeEducations",
                principalColumns: new[] { "EmployeeId", "EmployeeEducationId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDocuments_EmployeeIdentityDocuments_EmployeeId_EmployeeIdentityDocumentId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "EmployeeIdentityDocumentId" },
                principalTable: "EmployeeIdentityDocuments",
                principalColumns: new[] { "EmployeeId", "EmployeeIdentityDocumentId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDocuments_EmployeeCourseCertificates_EmployeeId_EmployeeCourseCertificateId",
                table: "EmployeeDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDocuments_EmployeeEducations_EmployeeId_EmployeeEducationId",
                table: "EmployeeDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDocuments_EmployeeIdentityDocuments_EmployeeId_EmployeeIdentityDocumentId",
                table: "EmployeeDocuments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EmployeeIdentityDocuments_EmployeeId_RecordId",
                table: "EmployeeIdentityDocuments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EmployeeEducations_EmployeeId_RecordId",
                table: "EmployeeEducations");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeId_EmployeeCourseCertificateId",
                table: "EmployeeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeId_EmployeeEducationId",
                table: "EmployeeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeId_EmployeeIdentityDocumentId",
                table: "EmployeeDocuments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EmployeeCourseCertificates_EmployeeId_RecordId",
                table: "EmployeeCourseCertificates");

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
    }
}
