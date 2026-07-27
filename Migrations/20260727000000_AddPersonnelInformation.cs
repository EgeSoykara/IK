using System;
using IK.Web.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(HumanResourcesDbContext))]
    [Migration("20260727000000_AddPersonnelInformation")]
    public partial class AddPersonnelInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeAddresses",
                columns: table => new
                {
                    EmployeeAddressId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    AddressType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAddresses", x => x.EmployeeAddressId);
                    table.ForeignKey(
                        name: "FK_EmployeeAddresses_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeBankAccounts",
                columns: table => new
                {
                    EmployeeBankAccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    BranchCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeBankAccounts", x => x.EmployeeBankAccountId);
                    table.ForeignKey(
                        name: "FK_EmployeeBankAccounts_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeCourseCertificates",
                columns: table => new
                {
                    EmployeeCourseCertificateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssuingOrganization = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CertificateNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCourseCertificates", x => x.EmployeeCourseCertificateId);
                    table.ForeignKey(
                        name: "FK_EmployeeCourseCertificates_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeEducations",
                columns: table => new
                {
                    EmployeeEducationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    InstitutionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepartmentName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Degree = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GraduationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsGraduated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeEducations", x => x.EmployeeEducationId);
                    table.ForeignKey(
                        name: "FK_EmployeeEducations_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeIdentityDocuments",
                columns: table => new
                {
                    EmployeeIdentityDocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IssuingAuthority = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeIdentityDocuments", x => x.EmployeeIdentityDocumentId);
                    table.ForeignKey(
                        name: "FK_EmployeeIdentityDocuments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePhones",
                columns: table => new
                {
                    EmployeePhoneId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PhoneType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePhones", x => x.EmployeePhoneId);
                    table.ForeignKey(
                        name: "FK_EmployeePhones_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeTerminations",
                columns: table => new
                {
                    EmployeeTerminationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    TerminationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTerminations", x => x.EmployeeTerminationId);
                    table.ForeignKey(
                        name: "FK_EmployeeTerminations_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAddresses_EmployeeId",
                table: "EmployeeAddresses",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBankAccounts_EmployeeId",
                table: "EmployeeBankAccounts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBankAccounts_Iban",
                table: "EmployeeBankAccounts",
                column: "Iban",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCourseCertificates_EmployeeId",
                table: "EmployeeCourseCertificates",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeEducations_EmployeeId",
                table: "EmployeeEducations",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeIdentityDocuments_EmployeeId",
                table: "EmployeeIdentityDocuments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeIdentityDocuments_EmployeeId_DocumentType_DocumentNumber",
                table: "EmployeeIdentityDocuments",
                columns: new[] { "EmployeeId", "DocumentType", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePhones_EmployeeId",
                table: "EmployeePhones",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTerminations_EmployeeId",
                table: "EmployeeTerminations",
                column: "EmployeeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeAddresses");

            migrationBuilder.DropTable(
                name: "EmployeeBankAccounts");

            migrationBuilder.DropTable(
                name: "EmployeeCourseCertificates");

            migrationBuilder.DropTable(
                name: "EmployeeEducations");

            migrationBuilder.DropTable(
                name: "EmployeeIdentityDocuments");

            migrationBuilder.DropTable(
                name: "EmployeePhones");

            migrationBuilder.DropTable(
                name: "EmployeeTerminations");
        }
    }
}
