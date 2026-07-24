using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeDocumentCategories",
                columns: table => new
                {
                    CanonicalKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDocumentCategories", x => x.CanonicalKey);
                    table.CheckConstraint("CK_EmployeeDocumentCategories_CanonicalKey", "[CanonicalKey] <> '' AND [CanonicalKey] COLLATE Latin1_General_100_BIN2 = LOWER([CanonicalKey]) AND [CanonicalKey] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^a-z0-9-]%' AND [CanonicalKey] NOT LIKE '-%' AND [CanonicalKey] NOT LIKE '%-' AND [CanonicalKey] NOT LIKE '%--%'");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeProfilePhotos",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeProfilePhotos", x => x.EmployeeId);
                    table.CheckConstraint("CK_EmployeeProfilePhotos_SizeBytes", "[SizeBytes] > 0");
                    table.ForeignKey(
                        name: "FK_EmployeeProfilePhotos_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDocuments",
                columns: table => new
                {
                    EmployeeDocumentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    CategoryCanonicalKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDocuments", x => x.EmployeeDocumentId);
                    table.CheckConstraint("CK_EmployeeDocuments_SizeBytes", "[SizeBytes] > 0");
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_EmployeeDocumentCategories_CategoryCanonicalKey",
                        column: x => x.CategoryCanonicalKey,
                        principalTable: "EmployeeDocumentCategories",
                        principalColumn: "CanonicalKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "EmployeeDocumentCategories",
                columns: new[] { "CanonicalKey", "DisplayName", "IsActive", "SortOrder" },
                values: new object[,]
                {
                    { "education", "Eğitim ve Sertifika Belgeleri", true, 30 },
                    { "employment", "İş ve Sözleşme Belgeleri", true, 20 },
                    { "health", "Sağlık Belgeleri", true, 40 },
                    { "identity", "Kimlik Belgeleri", true, 10 },
                    { "other", "Diğer Belgeler", true, 50 }
                });

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [sys].[check_constraints]
                    WHERE [name] = N'CK_AuditLogs_ActionType'
                      AND [parent_object_id] = OBJECT_ID(N'[dbo].[AuditLogs]'))
                BEGIN
                    ALTER TABLE [dbo].[AuditLogs]
                        DROP CONSTRAINT [CK_AuditLogs_ActionType];
                END;

                ALTER TABLE [dbo].[AuditLogs] WITH CHECK
                    ADD CONSTRAINT [CK_AuditLogs_ActionType]
                    CHECK ([ActionType] BETWEEN 1 AND 24);

                ALTER TABLE [dbo].[AuditLogs]
                    CHECK CONSTRAINT [CK_AuditLogs_ActionType];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_CategoryCanonicalKey",
                table: "EmployeeDocuments",
                column: "CategoryCanonicalKey");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_CategoryCanonicalKey_UploadedAt",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "CategoryCanonicalKey", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_StorageKey",
                table: "EmployeeDocuments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeProfilePhotos_StorageKey",
                table: "EmployeeProfilePhotos",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeDocuments");

            migrationBuilder.DropTable(
                name: "EmployeeProfilePhotos");

            migrationBuilder.DropTable(
                name: "EmployeeDocumentCategories");

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [sys].[check_constraints]
                    WHERE [name] = N'CK_AuditLogs_ActionType'
                      AND [parent_object_id] = OBJECT_ID(N'[dbo].[AuditLogs]'))
                BEGIN
                    ALTER TABLE [dbo].[AuditLogs]
                        DROP CONSTRAINT [CK_AuditLogs_ActionType];
                END;
                """);
        }
    }
}
