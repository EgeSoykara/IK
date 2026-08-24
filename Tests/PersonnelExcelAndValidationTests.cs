using System.Security.Claims;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IK.Web.Tests;

public sealed class PersonnelExcelAndValidationTests
{
    [Fact]
    public async Task PublicHolidayWorkbook_ExportsTypedDateAndImportsAtomically()
    {
        await using var db = CreateDbContext();
        db.PublicHolidays.Add(new PublicHoliday
        {
            Date = new DateOnly(2026, 11, 15),
            Name = "Cumhuriyet Bayramı"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var principal = HolidayManager();

        var workbook = await service.ExportAsync(
            PersonnelExcelDataset.PublicHolidays,
            principal,
            null,
            2026);
        using (var validationStream = new MemoryStream(workbook))
        using (var document = SpreadsheetDocument.Open(validationStream, false))
        {
            Assert.Empty(new OpenXmlValidator().Validate(document));
        }
        db.PublicHolidays.RemoveRange(db.PublicHolidays);
        await db.SaveChangesAsync();

        await using var stream = new AsyncOnlyReadStream(workbook);
        var result = await service.ImportAsync(
            PersonnelExcelDataset.PublicHolidays,
            principal,
            stream,
            null,
            2026,
            1);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(new DateOnly(2026, 11, 15), (await db.PublicHolidays.SingleAsync()).Date);

        await using var duplicateStream = new MemoryStream(workbook);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportAsync(
                PersonnelExcelDataset.PublicHolidays,
                principal,
                duplicateStream,
                null,
                2026,
                1));
        Assert.Single(await db.PublicHolidays.ToListAsync());
    }

    [Fact]
    public async Task EmployeeWorkbook_RoundTripsEmailAndDatabaseRole()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        db.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "İnsan Kaynakları"
        });
        db.Employees.Add(new Employee
        {
            EmployeeId = 1,
            ApplicationRoleId = ApplicationRoleDefaults.HumanResourcesRoleId,
            DepartmentId = 1,
            SicilNo = "IK-1",
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            SamAccountName = "ada.lovelace",
            KktcKimlikNo = "0000000001"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var principal = EmployeeManager();

        var workbook = await service.ExportAsync(
            PersonnelExcelDataset.Employees,
            principal,
            null,
            null);
        db.Employees.RemoveRange(db.Employees);
        await db.SaveChangesAsync();

        await using var stream = new MemoryStream(workbook);
        var result = await service.ImportAsync(
            PersonnelExcelDataset.Employees,
            principal,
            stream,
            null,
            null,
            99);

        Assert.Equal(1, result.ImportedCount);
        var employee = await db.Employees
            .Include(item => item.ApplicationRole)
            .SingleAsync();
        Assert.Equal("ada@example.com", employee.Email);
        Assert.Equal("ada.lovelace", employee.SamAccountName);
        Assert.Equal(ApplicationRoleDefaults.HumanResourcesName, employee.ApplicationRole.Name);
    }

    [Fact]
    public void CrossFieldValidators_RejectIncompleteEducationAndInvalidDates()
    {
        var educationErrors = PersonnelRecordValidator.ValidateEducation(
            null,
            null,
            true,
            null,
            null).ToList();
        var courseErrors = PersonnelRecordValidator.ValidateCourse(
            null,
            new DateTime(2026, 7, 10),
            new DateTime(2026, 7, 9),
            null).ToList();

        Assert.Contains(educationErrors, error => error.ErrorMessage == "Program/bölüm zorunludur.");
        Assert.Contains(educationErrors, error => error.ErrorMessage == "Mezun kaydı için mezuniyet tarihi zorunludur.");
        Assert.Contains(courseErrors, error => error.ErrorMessage == "Düzenleyen kurum zorunludur.");
        Assert.Contains(courseErrors, error => error.ErrorMessage == "Bitiş tarihi başlangıç tarihinden önce olamaz.");
    }

    private static PersonnelExcelService CreateService(HumanResourcesDbContext db)
    {
        var audit = new AuditLogService(db);
        var factory = TestHumanResourcesDbContextFactory.From(db);
        var pageAccessService = new PageAccessService(factory);
        return new PersonnelExcelService(
            db,
            pageAccessService,
            new PersonnelAuthorizationService(factory, pageAccessService),
            audit);
    }

    private static ClaimsPrincipal HolidayManager() =>
        new(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(PermissionClaimTypes.Permission, PermissionNames.CanManagePublicHolidays)
            ],
            "Test"));

    private static ClaimsPrincipal EmployeeManager() =>
        new(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(PermissionClaimTypes.Permission, PermissionNames.CanCreateNewEmployee)
            ],
            "Test"));

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new HumanResourcesDbContext(options);
    }

    private sealed class AsyncOnlyReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content);

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("Synchronous reads are not supported.");

        public override int Read(Span<byte> buffer) =>
            throw new NotSupportedException("Synchronous reads are not supported.");

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
