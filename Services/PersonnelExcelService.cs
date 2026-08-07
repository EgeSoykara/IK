using System.Globalization;
using System.Security.Claims;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using IK.Web.Components;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public enum PersonnelExcelDataset
{
    PublicHolidays,
    Employees,
    BankAccounts,
    IdentityDocuments,
    Phones,
    Addresses,
    Educations,
    CourseCertificates
}

public sealed record PersonnelExcelImportResult(int ImportedCount);

public sealed class PersonnelExcelService(
    HumanResourcesDbContext dbContext,
    PageAccessService pageAccessService,
    AuditLogService auditLogService)
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public const int MaxRowCount = 2_000;

    private static readonly IReadOnlyDictionary<PersonnelExcelDataset, string[]> Headers =
        new Dictionary<PersonnelExcelDataset, string[]>
        {
            [PersonnelExcelDataset.PublicHolidays] = ["Tarih", "Tatil Adı"],
            [PersonnelExcelDataset.Employees] =
            [
                "Sicil No", "Ad", "Soyad", "KKTC Kimlik No", "Departman",
                "İşe Başlama Tarihi", "Kadro Tarihi", "Cinsiyet", "Kan Grubu", "Durum"
            ],
            [PersonnelExcelDataset.BankAccounts] =
                ["Banka", "Şube", "Şube Kodu", "Hesap Numarası", "IBAN", "Birincil"],
            [PersonnelExcelDataset.IdentityDocuments] =
                ["Belge Türü", "Belge Numarası", "Düzenleyen Kurum", "Düzenlenme Tarihi", "Son Geçerlilik Tarihi", "Açıklama"],
            [PersonnelExcelDataset.Phones] =
                ["Telefon Türü", "Telefon Numarası", "Dahili", "Birincil"],
            [PersonnelExcelDataset.Addresses] =
                ["Adres Türü", "Ülke", "Şehir", "İlçe/Bölge", "Posta Kodu", "Adres", "Birincil"],
            [PersonnelExcelDataset.Educations] =
                ["Kurum/Okul", "Program/Bölüm", "Eğitim Seviyesi", "Diploma/Derece", "Başlangıç Tarihi", "Mezuniyet Tarihi", "Mezun"],
            [PersonnelExcelDataset.CourseCertificates] =
                ["Kurs/Sertifika Adı", "Düzenleyen Kurum", "Başlangıç Tarihi", "Bitiş Tarihi", "Belge Numarası", "Geçerlilik Tarihi"]
        };

    public async Task<byte[]> ExportAsync(
        PersonnelExcelDataset dataset,
        ClaimsPrincipal principal,
        int? employeeId,
        int? year,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(dataset, principal, employeeId);
        var rows = await LoadExportRowsAsync(dataset, employeeId, year, cancellationToken);
        return CreateWorkbook(dataset, rows);
    }

    public async Task<PersonnelExcelImportResult> ImportAsync(
        PersonnelExcelDataset dataset,
        ClaimsPrincipal principal,
        Stream workbook,
        int? employeeId,
        int? year,
        int actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(dataset, principal, employeeId);
        var rows = await ReadWorkbookAsync(dataset, workbook, cancellationToken);
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("Excel dosyasında içe aktarılacak veri satırı bulunamadı.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        switch (dataset)
        {
            case PersonnelExcelDataset.PublicHolidays:
                await ImportPublicHolidaysAsync(rows, year, cancellationToken);
                break;
            case PersonnelExcelDataset.Employees:
                await ImportEmployeesAsync(rows, cancellationToken);
                break;
            case PersonnelExcelDataset.BankAccounts:
                await ImportBankAccountsAsync(employeeId!.Value, rows, cancellationToken);
                break;
            case PersonnelExcelDataset.IdentityDocuments:
                await ImportIdentityDocumentsAsync(employeeId!.Value, rows, cancellationToken);
                break;
            case PersonnelExcelDataset.Phones:
                await ImportPhonesAsync(employeeId!.Value, rows, cancellationToken);
                break;
            case PersonnelExcelDataset.Addresses:
                await ImportAddressesAsync(employeeId!.Value, rows, cancellationToken);
                break;
            case PersonnelExcelDataset.Educations:
                await ImportEducationsAsync(employeeId!.Value, rows, cancellationToken);
                break;
            case PersonnelExcelDataset.CourseCertificates:
                await ImportCoursesAsync(employeeId!.Value, rows, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(dataset));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.AppendAsync(
            AuditActionType.PersonnelExcelImported,
            dataset.ToString(),
            employeeId?.ToString() ?? year?.ToString() ?? "all",
            actorEmployeeId,
            $"Excel içe aktarma tamamlandı; satır sayısı={rows.Count}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PersonnelExcelImportResult(rows.Count);
    }

    private void EnsureAuthorized(
        PersonnelExcelDataset dataset,
        ClaimsPrincipal principal,
        int? employeeId)
    {
        if (dataset == PersonnelExcelDataset.PublicHolidays)
        {
            if (!pageAccessService.CanManagePublicHolidays(principal))
            {
                throw new UnauthorizedAccessException("Resmî tatil Excel işlemi için yetkiniz yok.");
            }
            return;
        }

        if (dataset == PersonnelExcelDataset.Employees)
        {
            if (!pageAccessService.CanManageEmployees(principal))
            {
                throw new UnauthorizedAccessException("Çalışan Excel işlemi için yetkiniz yok.");
            }
            return;
        }

        if (!employeeId.HasValue
            || !pageAccessService.CanEditPersonnelInformation(principal, employeeId.Value))
        {
            throw new UnauthorizedAccessException("Bu çalışanın Excel işlemi için yetkiniz yok.");
        }
    }

    private async Task<List<object?[]>> LoadExportRowsAsync(
        PersonnelExcelDataset dataset,
        int? employeeId,
        int? year,
        CancellationToken cancellationToken)
    {
        switch (dataset)
        {
            case PersonnelExcelDataset.PublicHolidays:
            {
                var selectedYear = year ?? DateTime.Today.Year;
                var start = new DateOnly(selectedYear, 1, 1);
                var end = new DateOnly(selectedYear, 12, 31);
                return (await dbContext.PublicHolidays.AsNoTracking()
                        .Where(item => item.Date >= start && item.Date <= end)
                        .OrderBy(item => item.Date)
                        .ToListAsync(cancellationToken))
                    .Select(item => new object?[] { item.Date, item.Name }).ToList();
            }
            case PersonnelExcelDataset.Employees:
                return (await dbContext.Employees.AsNoTracking()
                        .Include(item => item.Department)
                        .OrderBy(item => item.SicilNo)
                        .ToListAsync(cancellationToken))
                    .Select(item => new object?[]
                    {
                        item.SicilNo, item.FirstName, item.LastName, item.KktcKimlikNo,
                        item.Department.DepartmentName, ToDateOnly(item.StartDate),
                        ToDateOnly(item.StaffDate), item.Gender?.ToString(),
                        item.BloodGroup?.ToString(), item.Status.ToString()
                    }).ToList();
            case PersonnelExcelDataset.BankAccounts:
                return (await dbContext.EmployeeBankAccounts.AsNoTracking()
                        .Where(item => item.EmployeeId == employeeId)
                        .OrderByDescending(item => item.IsPrimary).ThenBy(item => item.BankName)
                        .ToListAsync(cancellationToken))
                    .Select(item => new object?[]
                        { item.BankName, item.BranchName, item.BranchCode, item.AccountNumber, item.Iban, item.IsPrimary }).ToList();
            case PersonnelExcelDataset.IdentityDocuments:
                return (await dbContext.EmployeeIdentityDocuments.AsNoTracking()
                        .Where(item => item.EmployeeId == employeeId)
                        .OrderBy(item => item.DocumentType).ThenBy(item => item.DocumentNumber)
                        .ToListAsync(cancellationToken))
                    .Select(item => new object?[]
                        { item.DocumentType, item.DocumentNumber, item.IssuingAuthority, item.IssueDate, item.ExpiryDate, item.Description }).ToList();
            case PersonnelExcelDataset.Phones:
                return (await dbContext.EmployeePhones.AsNoTracking()
                        .Where(item => item.EmployeeId == employeeId)
                        .OrderByDescending(item => item.IsPrimary).ThenBy(item => item.PhoneType)
                        .ToListAsync(cancellationToken))
                    .Select(item => new object?[]
                        { item.PhoneType, item.PhoneNumber, item.Extension, item.IsPrimary }).ToList();
            case PersonnelExcelDataset.Addresses:
                return (await dbContext.EmployeeAddresses.AsNoTracking()
                        .Where(item => item.EmployeeId == employeeId)
                        .OrderByDescending(item => item.IsPrimary).ThenBy(item => item.AddressType)
                        .ToListAsync(cancellationToken))
                    .Select(item => new object?[]
                        { item.AddressType, item.Country, item.City, item.District, item.PostalCode, item.AddressLine, item.IsPrimary }).ToList();
            case PersonnelExcelDataset.Educations:
                return (await dbContext.EmployeeEducations.AsNoTracking()
                        .Where(item => item.EmployeeId == employeeId)
                        .OrderByDescending(item => item.StartDate)
                        .ToListAsync(cancellationToken))
                    .Select(item => new object?[]
                        { item.InstitutionName, item.DepartmentName, item.EducationLevel, item.Degree, item.StartDate, item.GraduationDate, item.IsGraduated }).ToList();
            case PersonnelExcelDataset.CourseCertificates:
                return (await dbContext.EmployeeCourseCertificates.AsNoTracking()
                        .Where(item => item.EmployeeId == employeeId)
                        .OrderByDescending(item => item.StartDate)
                        .ToListAsync(cancellationToken))
                    .Select(item => new object?[]
                        { item.Name, item.IssuingOrganization, item.StartDate, item.EndDate, item.CertificateNumber, item.ExpiryDate }).ToList();
            default:
                throw new ArgumentOutOfRangeException(nameof(dataset));
        }
    }

    private async Task ImportPublicHolidaysAsync(
        IReadOnlyList<RowData> rows,
        int? selectedYear,
        CancellationToken cancellationToken)
    {
        var parsed = rows.Select(row => new
        {
            Row = row.Number,
            Date = RequiredDate(row, "Tarih"),
            Name = Required(row, "Tatil Adı", 120)
        }).ToList();
        if (selectedYear.HasValue && parsed.Any(item => item.Date.Year != selectedYear.Value))
        {
            throw new InvalidOperationException($"Tüm tarihler seçili {selectedYear.Value} yılına ait olmalıdır.");
        }
        EnsureDistinct(parsed, item => item.Date, "Aynı tarih Excel dosyasında birden fazla kez bulunamaz.");
        var dates = parsed.Select(item => item.Date).ToList();
        if (await dbContext.PublicHolidays.AnyAsync(item => dates.Contains(item.Date), cancellationToken))
        {
            throw new InvalidOperationException("Excel dosyasındaki tarihlerden en az biri sistemde zaten kayıtlı.");
        }
        dbContext.PublicHolidays.AddRange(parsed.Select(item => new PublicHoliday { Date = item.Date, Name = item.Name }));
    }

    private async Task ImportEmployeesAsync(
        IReadOnlyList<RowData> rows,
        CancellationToken cancellationToken)
    {
        var departments = await dbContext.Departments.AsNoTracking().ToDictionaryAsync(
            item => item.DepartmentName,
            StringComparer.OrdinalIgnoreCase,
            cancellationToken);
        var employees = new List<Employee>();
        foreach (var row in rows)
        {
            var departmentName = Required(row, "Departman", 120);
            if (!departments.TryGetValue(departmentName, out var department))
            {
                throw RowError(row, "Departman", $"'{departmentName}' adlı departman bulunamadı.");
            }
            var employee = new Employee
            {
                SicilNo = Required(row, "Sicil No", 30),
                FirstName = Required(row, "Ad", 80),
                LastName = Required(row, "Soyad", 80),
                KktcKimlikNo = Required(row, "KKTC Kimlik No", 10),
                DepartmentId = department.DepartmentId,
                StartDate = OptionalDate(row, "İşe Başlama Tarihi")?.ToDateTime(TimeOnly.MinValue),
                StaffDate = OptionalDate(row, "Kadro Tarihi")?.ToDateTime(TimeOnly.MinValue),
                Gender = OptionalEnum<EmployeeGender>(row, "Cinsiyet"),
                BloodGroup = OptionalEnum<BloodGroup>(row, "Kan Grubu"),
                Status = OptionalEnum<EmploymentStatus>(row, "Durum") ?? EmploymentStatus.Active
            };
            if (employee.KktcKimlikNo.Length != 10)
            {
                throw RowError(row, "KKTC Kimlik No", "KKTC Kimlik No tam olarak 10 karakter olmalıdır.");
            }
            employee.ManagerId = department.ManagerEmployeeId;
            employees.Add(employee);
        }
        EnsureDistinct(employees, item => item.SicilNo, "Sicil No Excel dosyasında benzersiz olmalıdır.");
        EnsureDistinct(employees, item => item.KktcKimlikNo, "KKTC Kimlik No Excel dosyasında benzersiz olmalıdır.");
        var sicilNumbers = employees.Select(item => item.SicilNo).ToList();
        var identityNumbers = employees.Select(item => item.KktcKimlikNo).ToList();
        if (await dbContext.Employees.AnyAsync(
                item => sicilNumbers.Contains(item.SicilNo) || identityNumbers.Contains(item.KktcKimlikNo),
                cancellationToken))
        {
            throw new InvalidOperationException("Excel dosyasındaki sicil veya kimlik numaralarından en az biri sistemde zaten kayıtlı.");
        }
        dbContext.Employees.AddRange(employees);
    }

    private async Task ImportBankAccountsAsync(int employeeId, IReadOnlyList<RowData> rows, CancellationToken cancellationToken)
    {
        var accounts = rows.Select(row => new EmployeeBankAccount
        {
            EmployeeId = employeeId,
            BankName = Required(row, "Banka", 120),
            BranchName = Optional(row, "Şube", 120),
            BranchCode = Optional(row, "Şube Kodu", 30),
            AccountNumber = Optional(row, "Hesap Numarası", 50),
            Iban = Required(row, "IBAN", 34),
            IsPrimary = Boolean(row, "Birincil")
        }).ToList();
        EnsureDistinct(accounts, item => item.Iban, "IBAN Excel dosyasında benzersiz olmalıdır.");
        if (accounts.Count(item => item.IsPrimary) > 1
            || (accounts.Any(item => item.IsPrimary)
                && await dbContext.EmployeeBankAccounts.AnyAsync(item => item.EmployeeId == employeeId && item.IsPrimary, cancellationToken)))
        {
            throw new InvalidOperationException("Bir çalışanın yalnız bir birincil banka hesabı olabilir.");
        }
        var ibans = accounts.Select(item => item.Iban).ToList();
        if (await dbContext.EmployeeBankAccounts.AnyAsync(item => ibans.Contains(item.Iban), cancellationToken))
        {
            throw new InvalidOperationException("Excel dosyasındaki IBAN'lardan en az biri sistemde zaten kayıtlı.");
        }
        dbContext.EmployeeBankAccounts.AddRange(accounts);
    }

    private async Task ImportIdentityDocumentsAsync(int employeeId, IReadOnlyList<RowData> rows, CancellationToken cancellationToken)
    {
        var documents = new List<EmployeeIdentityDocument>();
        foreach (var row in rows)
        {
            var type = Required(row, "Belge Türü", 80);
            EnsureConfiguredOption(row, "Belge Türü", type, PersonnelSelectOptions.DocumentTypes);
            var authority = Required(row, "Düzenleyen Kurum", 120);
            var issueDate = RequiredDate(row, "Düzenlenme Tarihi");
            var expiryDate = OptionalDate(row, "Son Geçerlilik Tarihi");
            EnsureNoErrors(row, PersonnelRecordValidator.ValidateIdentityDocument(
                authority,
                issueDate.ToDateTime(TimeOnly.MinValue),
                expiryDate?.ToDateTime(TimeOnly.MinValue)));
            documents.Add(new EmployeeIdentityDocument
            {
                EmployeeId = employeeId,
                DocumentType = type,
                DocumentNumber = Required(row, "Belge Numarası", 80),
                IssuingAuthority = authority,
                IssueDate = issueDate,
                ExpiryDate = expiryDate,
                Description = Optional(row, "Açıklama", 500)
            });
        }
        EnsureDistinct(documents, item => $"{item.DocumentType}\u001f{item.DocumentNumber}", "Belge türü ve numarası Excel dosyasında benzersiz olmalıdır.");
        foreach (var document in documents)
        {
            if (await dbContext.EmployeeIdentityDocuments.AnyAsync(
                    item => item.EmployeeId == employeeId
                            && item.DocumentType == document.DocumentType
                            && item.DocumentNumber == document.DocumentNumber,
                    cancellationToken))
            {
                throw new InvalidOperationException("Excel dosyasındaki belgelerden en az biri sistemde zaten kayıtlı.");
            }
        }
        dbContext.EmployeeIdentityDocuments.AddRange(documents);
    }

    private async Task ImportPhonesAsync(int employeeId, IReadOnlyList<RowData> rows, CancellationToken cancellationToken)
    {
        var phones = new List<EmployeePhone>();
        foreach (var row in rows)
        {
            var type = Required(row, "Telefon Türü", 40);
            EnsureConfiguredOption(row, "Telefon Türü", type, PersonnelSelectOptions.PhoneTypes);
            phones.Add(new EmployeePhone
            {
                EmployeeId = employeeId,
                PhoneType = type,
                PhoneNumber = Required(row, "Telefon Numarası", 30),
                Extension = Optional(row, "Dahili", 10),
                IsPrimary = Boolean(row, "Birincil")
            });
        }
        EnsureDistinct(phones, item => $"{item.PhoneType}\u001f{item.PhoneNumber}", "Telefon türü ve numarası Excel dosyasında benzersiz olmalıdır.");
        if (phones.Count(item => item.IsPrimary) > 1
            || (phones.Any(item => item.IsPrimary)
                && await dbContext.EmployeePhones.AnyAsync(item => item.EmployeeId == employeeId && item.IsPrimary, cancellationToken)))
        {
            throw new InvalidOperationException("Bir çalışanın yalnız bir birincil telefonu olabilir.");
        }
        foreach (var phone in phones)
        {
            if (await dbContext.EmployeePhones.AnyAsync(
                    item => item.EmployeeId == employeeId
                            && item.PhoneType == phone.PhoneType
                            && item.PhoneNumber == phone.PhoneNumber,
                    cancellationToken))
            {
                throw new InvalidOperationException("Excel dosyasındaki telefonlardan en az biri sistemde zaten kayıtlı.");
            }
        }
        dbContext.EmployeePhones.AddRange(phones);
    }

    private async Task ImportAddressesAsync(int employeeId, IReadOnlyList<RowData> rows, CancellationToken cancellationToken)
    {
        var addresses = new List<EmployeeAddress>();
        foreach (var row in rows)
        {
            var type = Required(row, "Adres Türü", 40);
            EnsureConfiguredOption(row, "Adres Türü", type, PersonnelSelectOptions.AddressTypes);
            var country = Required(row, "Ülke", 100);
            var city = Required(row, "Şehir", 100);
            var district = Required(row, "İlçe/Bölge", 100);
            EnsureNoErrors(row, PersonnelRecordValidator.ValidateAddress(country, city, district));
            addresses.Add(new EmployeeAddress
            {
                EmployeeId = employeeId,
                AddressType = type,
                Country = country,
                City = city,
                District = district,
                PostalCode = Optional(row, "Posta Kodu", 20),
                AddressLine = Required(row, "Adres", 300),
                IsPrimary = Boolean(row, "Birincil")
            });
        }
        EnsureDistinct(addresses, item => $"{item.AddressType}\u001f{item.Country}\u001f{item.City}\u001f{item.District}\u001f{item.AddressLine}", "Adresler Excel dosyasında benzersiz olmalıdır.");
        if (addresses.Count(item => item.IsPrimary) > 1
            || (addresses.Any(item => item.IsPrimary)
                && await dbContext.EmployeeAddresses.AnyAsync(item => item.EmployeeId == employeeId && item.IsPrimary, cancellationToken)))
        {
            throw new InvalidOperationException("Bir çalışanın yalnız bir birincil adresi olabilir.");
        }
        foreach (var address in addresses)
        {
            if (await dbContext.EmployeeAddresses.AnyAsync(
                    item => item.EmployeeId == employeeId
                            && item.AddressType == address.AddressType
                            && item.AddressLine == address.AddressLine
                            && item.City == address.City,
                    cancellationToken))
            {
                throw new InvalidOperationException("Excel dosyasındaki adreslerden en az biri sistemde zaten kayıtlı.");
            }
        }
        dbContext.EmployeeAddresses.AddRange(addresses);
    }

    private async Task ImportEducationsAsync(int employeeId, IReadOnlyList<RowData> rows, CancellationToken cancellationToken)
    {
        var educations = new List<EmployeeEducation>();
        foreach (var row in rows)
        {
            var level = Required(row, "Eğitim Seviyesi", 80);
            EnsureConfiguredOption(row, "Eğitim Seviyesi", level, PersonnelSelectOptions.EducationLevels);
            var program = Required(row, "Program/Bölüm", 160);
            var start = RequiredDate(row, "Başlangıç Tarihi");
            var graduated = Boolean(row, "Mezun");
            var degree = Optional(row, "Diploma/Derece", 120);
            var graduation = OptionalDate(row, "Mezuniyet Tarihi");
            EnsureNoErrors(row, PersonnelRecordValidator.ValidateEducation(
                program,
                start.ToDateTime(TimeOnly.MinValue),
                graduated,
                degree,
                graduation?.ToDateTime(TimeOnly.MinValue)));
            educations.Add(new EmployeeEducation
            {
                EmployeeId = employeeId,
                InstitutionName = Required(row, "Kurum/Okul", 200),
                DepartmentName = program,
                EducationLevel = level,
                Degree = degree,
                StartDate = start,
                GraduationDate = graduation,
                IsGraduated = graduated
            });
        }
        EnsureDistinct(educations, item => $"{item.InstitutionName}\u001f{item.DepartmentName}\u001f{item.EducationLevel}\u001f{item.StartDate}", "Eğitim kayıtları Excel dosyasında benzersiz olmalıdır.");
        foreach (var education in educations)
        {
            if (await dbContext.EmployeeEducations.AnyAsync(
                    item => item.EmployeeId == employeeId
                            && item.InstitutionName == education.InstitutionName
                            && item.DepartmentName == education.DepartmentName
                            && item.EducationLevel == education.EducationLevel
                            && item.StartDate == education.StartDate,
                    cancellationToken))
            {
                throw new InvalidOperationException("Excel dosyasındaki eğitimlerden en az biri sistemde zaten kayıtlı.");
            }
        }
        dbContext.EmployeeEducations.AddRange(educations);
    }

    private async Task ImportCoursesAsync(int employeeId, IReadOnlyList<RowData> rows, CancellationToken cancellationToken)
    {
        var courses = new List<EmployeeCourseCertificate>();
        foreach (var row in rows)
        {
            var organization = Required(row, "Düzenleyen Kurum", 160);
            var start = RequiredDate(row, "Başlangıç Tarihi");
            var end = OptionalDate(row, "Bitiş Tarihi");
            var expiry = OptionalDate(row, "Geçerlilik Tarihi");
            EnsureNoErrors(row, PersonnelRecordValidator.ValidateCourse(
                organization,
                start.ToDateTime(TimeOnly.MinValue),
                end?.ToDateTime(TimeOnly.MinValue),
                expiry?.ToDateTime(TimeOnly.MinValue)));
            courses.Add(new EmployeeCourseCertificate
            {
                EmployeeId = employeeId,
                Name = Required(row, "Kurs/Sertifika Adı", 200),
                IssuingOrganization = organization,
                StartDate = start,
                EndDate = end,
                CertificateNumber = Optional(row, "Belge Numarası", 100),
                ExpiryDate = expiry
            });
        }
        EnsureDistinct(courses, item => $"{item.Name}\u001f{item.IssuingOrganization}\u001f{item.StartDate}", "Kurs/sertifika kayıtları Excel dosyasında benzersiz olmalıdır.");
        foreach (var course in courses)
        {
            if (await dbContext.EmployeeCourseCertificates.AnyAsync(
                    item => item.EmployeeId == employeeId
                            && item.Name == course.Name
                            && item.IssuingOrganization == course.IssuingOrganization
                            && item.StartDate == course.StartDate,
                    cancellationToken))
            {
                throw new InvalidOperationException("Excel dosyasındaki kurs/sertifikalardan en az biri sistemde zaten kayıtlı.");
            }
        }
        dbContext.EmployeeCourseCertificates.AddRange(courses);
    }

    private static byte[] CreateWorkbook(PersonnelExcelDataset dataset, IReadOnlyList<object?[]> rows)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = CreateStylesheet();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var headerRow = new Row();
            foreach (var header in Headers[dataset])
            {
                headerRow.Append(TextCell(header, 2));
            }
            sheetData.Append(headerRow);

            foreach (var values in rows)
            {
                var row = new Row();
                foreach (var value in values)
                {
                    row.Append(value switch
                    {
                        DateOnly date => DateCell(date),
                        DateTime date => DateCell(DateOnly.FromDateTime(date)),
                        bool boolean => TextCell(boolean ? "Evet" : "Hayır"),
                        null => TextCell(string.Empty),
                        _ => TextCell(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
                    });
                }
                sheetData.Append(row);
            }

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Veriler"
            });
            workbookPart.Workbook.Save();
        }
        return stream.ToArray();
    }

    private static async Task<List<RowData>> ReadWorkbookAsync(
        PersonnelExcelDataset dataset,
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken);
        if (copy.Length == 0 || copy.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("Excel dosyası boş veya 5 MB sınırını aşıyor.");
        }
        copy.Position = 0;

        using var document = SpreadsheetDocument.Open(
            copy,
            false,
            new OpenSettings { MaxCharactersInPart = 2_000_000 });
        if (document.DocumentType != SpreadsheetDocumentType.Workbook)
        {
            throw new InvalidOperationException("Yalnız makro içermeyen .xlsx çalışma kitapları kabul edilir.");
        }

        var workbookPart = document.WorkbookPart
            ?? throw new InvalidOperationException("Excel çalışma kitabı okunamadı.");
        if (workbookPart.ExternalRelationships.Any()
            || document.GetAllParts().Any(part => part.ExternalRelationships.Any()))
        {
            throw new InvalidOperationException("Dış bağlantı içeren Excel dosyaları kabul edilmez.");
        }
        var workbookModel = workbookPart.Workbook
            ?? throw new InvalidOperationException("Excel çalışma kitabı modeli bulunamadı.");
        var sheets = workbookModel.Sheets?.Elements<Sheet>().ToList() ?? [];
        if (sheets.Count != 1)
        {
            throw new InvalidOperationException("Excel dosyasında yalnız bir çalışma sayfası bulunmalıdır.");
        }
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheets[0].Id!);
        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidOperationException("Excel çalışma sayfası okunamadı.");
        var sourceRows = worksheet.GetFirstChild<SheetData>()?.Elements<Row>().ToList() ?? [];
        if (sourceRows.Count == 0)
        {
            throw new InvalidOperationException("Excel dosyasında başlık satırı bulunamadı.");
        }
        if (sourceRows.Count - 1 > MaxRowCount)
        {
            throw new InvalidOperationException($"Excel dosyası en fazla {MaxRowCount} veri satırı içerebilir.");
        }
        if (sourceRows.SelectMany(item => item.Elements<Cell>()).Any(cell => cell.CellFormula is not null))
        {
            throw new InvalidOperationException("Formül içeren Excel dosyaları kabul edilmez.");
        }

        var expected = Headers[dataset];
        var actual = ReadRowValues(workbookPart, sourceRows[0], expected.Length, 1);
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Excel başlıkları beklenen sırada olmalıdır: {string.Join(", ", expected)}.");
        }

        var result = new List<RowData>();
        for (var index = 1; index < sourceRows.Count; index++)
        {
            var values = ReadRowValues(workbookPart, sourceRows[index], expected.Length, index + 1);
            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }
            result.Add(new RowData(index + 1, expected.Zip(values).ToDictionary(item => item.First, item => item.Second)));
        }
        return result;
    }

    private static Stylesheet CreateStylesheet() =>
        new(
            new Fonts(new Font(), new Font(new Bold())),
            new Fills(new Fill(new PatternFill { PatternType = PatternValues.None })),
            new Borders(new Border()),
            new CellStyleFormats(new CellFormat()),
            new CellFormats(
                new CellFormat(),
                new CellFormat { NumberFormatId = 14, ApplyNumberFormat = true },
                new CellFormat { FontId = 1, ApplyFont = true }));

    private static Cell TextCell(string value, uint styleIndex = 0) =>
        new()
        {
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(value)),
            StyleIndex = styleIndex
        };

    private static Cell DateCell(DateOnly value) =>
        new()
        {
            DataType = CellValues.Number,
            CellValue = new CellValue(value.ToDateTime(TimeOnly.MinValue).ToOADate().ToString(CultureInfo.InvariantCulture)),
            StyleIndex = 1
        };

    private static string ReadCell(WorkbookPart workbookPart, Cell cell)
    {
        var value = cell.CellValue?.InnerText ?? cell.InlineString?.InnerText ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString
            && int.TryParse(value, out var sharedIndex))
        {
            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
            return sharedStrings?.Elements<SharedStringItem>().ElementAt(sharedIndex).InnerText
                   ?? string.Empty;
        }
        if (cell.DataType?.Value == CellValues.Boolean)
        {
            return value == "1" ? "Evet" : "Hayır";
        }
        return value.Trim();
    }

    private static string[] ReadRowValues(
        WorkbookPart workbookPart,
        Row row,
        int columnCount,
        int rowNumber)
    {
        var values = Enumerable.Repeat(string.Empty, columnCount).ToArray();
        var seenColumns = new bool[columnCount];
        var nextSequentialColumn = 0;
        foreach (var cell in row.Elements<Cell>())
        {
            var column = cell.CellReference?.Value is { Length: > 0 } reference
                ? GetColumnIndex(reference)
                : nextSequentialColumn;
            if (column < 0 || column >= columnCount)
            {
                throw new InvalidOperationException($"{rowNumber}. satırda beklenmeyen ek sütun bulunuyor.");
            }
            if (seenColumns[column])
            {
                throw new InvalidOperationException($"{rowNumber}. satırda aynı sütun birden fazla kez bulunuyor.");
            }
            values[column] = ReadCell(workbookPart, cell);
            seenColumns[column] = true;
            nextSequentialColumn = column + 1;
        }
        return values;
    }

    private static int GetColumnIndex(string cellReference)
    {
        var index = 0;
        var letterCount = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsAsciiLetter(character))
            {
                break;
            }
            index = checked(index * 26 + (char.ToUpperInvariant(character) - 'A' + 1));
            letterCount++;
        }
        return letterCount == 0 ? -1 : index - 1;
    }

    private static string Required(RowData row, string column, int maxLength)
    {
        var value = row.Values[column].Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw RowError(row, column, "Bu alan zorunludur.");
        }
        if (value.Length > maxLength)
        {
            throw RowError(row, column, $"En fazla {maxLength} karakter olabilir.");
        }
        return value;
    }

    private static string? Optional(RowData row, string column, int maxLength)
    {
        var value = row.Values[column].Trim();
        if (value.Length > maxLength)
        {
            throw RowError(row, column, $"En fazla {maxLength} karakter olabilir.");
        }
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateOnly RequiredDate(RowData row, string column) =>
        OptionalDate(row, column) ?? throw RowError(row, column, "Tarih zorunludur.");

    private static DateOnly? OptionalDate(RowData row, string column)
    {
        var value = row.Values[column].Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
        {
            return iso;
        }
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
        {
            try
            {
                return DateOnly.FromDateTime(DateTime.FromOADate(serial));
            }
            catch (ArgumentException)
            {
            }
        }
        throw RowError(row, column, "Tarih Excel tarihi veya yyyy-MM-dd biçiminde olmalıdır.");
    }

    private static bool Boolean(RowData row, string column)
    {
        var value = row.Values[column].Trim();
        if (value.Equals("Evet", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value == "1")
        {
            return true;
        }
        if (value.Equals("Hayır", StringComparison.OrdinalIgnoreCase)
            || value.Equals("false", StringComparison.OrdinalIgnoreCase)
            || value == "0")
        {
            return false;
        }
        throw RowError(row, column, "Değer Evet veya Hayır olmalıdır.");
    }

    private static TEnum? OptionalEnum<TEnum>(RowData row, string column)
        where TEnum : struct, Enum
    {
        var value = row.Values[column].Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (Enum.TryParse<TEnum>(value, true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }
        throw RowError(row, column, "Geçersiz seçenek.");
    }

    private static void EnsureConfiguredOption(
        RowData row,
        string column,
        string value,
        IReadOnlyList<string> options)
    {
        if (!options.Contains(value, StringComparer.Ordinal))
        {
            throw RowError(row, column, "Değer sistemde tanımlı seçeneklerden biri olmalıdır.");
        }
    }

    private static void EnsureNoErrors(
        RowData row,
        IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> results)
    {
        var first = results.FirstOrDefault();
        if (first is not null)
        {
            throw new InvalidOperationException($"{row.Number}. satır: {first.ErrorMessage}");
        }
    }

    private static void EnsureDistinct<T, TKey>(
        IEnumerable<T> items,
        Func<T, TKey> keySelector,
        string error)
        where TKey : notnull
    {
        if (items.GroupBy(keySelector).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException(error);
        }
    }

    private static InvalidOperationException RowError(RowData row, string column, string message) =>
        new($"{row.Number}. satır, '{column}' sütunu: {message}");

    private static DateOnly? ToDateOnly(DateTime? value) =>
        value.HasValue ? DateOnly.FromDateTime(value.Value) : null;

    private sealed record RowData(int Number, IReadOnlyDictionary<string, string> Values);
}
