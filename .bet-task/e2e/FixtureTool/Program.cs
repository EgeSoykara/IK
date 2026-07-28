using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

var mode = args.SingleOrDefault();
if (mode is not ("validate" or "validate-migration-guards" or "setup" or "teardown" or "verify-delegation-lifecycle"))
{
    throw new InvalidOperationException(
        "FixtureTool requires validate, validate-migration-guards, setup, teardown or verify-delegation-lifecycle.");
}

var connectionString = Environment.GetEnvironmentVariable("IK_E2E_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("IK_E2E_CONNECTION_STRING is required.");
}

var connection = new SqlConnectionStringBuilder(connectionString);
if (string.IsNullOrWhiteSpace(connection.InitialCatalog)
    || !connection.InitialCatalog.StartsWith("IK_E2E_", StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "E2E database name must start with IK_E2E_; refusing to mutate a shared database.");
}

if (mode == "validate")
{
    return;
}

var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
    .UseSqlServer(connection.ConnectionString)
    .Options;
await using var database = new HumanResourcesDbContext(options);

if (mode == "validate-migration-guards")
{
    await ValidateMigrationGuardsAsync(options);
    return;
}

if (mode == "teardown")
{
    await database.Database.EnsureDeletedAsync();
    return;
}

if (mode == "verify-delegation-lifecycle")
{
    var managerLeave = await database.LeaveRequests.SingleAsync(request =>
        request.EmployeeId == 2
        && request.DelegateEmployeeId == 1
        && request.CurrentStatus == LeaveRequestStatus.Approved);
    var leaveStart = DateOnly.FromDateTime(managerLeave.StartDate!.Value);
    var leaveEnd = DateOnly.FromDateTime(managerLeave.EndDate!.Value);
    var delegationService = new ManagerDelegationService(database, new AuditLogService(database));
    await delegationService.ReconcileAsync(leaveStart);

    var activeDelegation = await database.ManagerDelegations.SingleAsync(item =>
        item.LeaveRequestId == managerLeave.RequestId);
    var pendingLifecycleRequest = await database.LeaveRequests.SingleAsync(request =>
        request.Reason == "E2E bekleyen yönetici onayı");
    var worker = await database.Employees.SingleAsync(employee => employee.SicilNo == "E2E-WORKER");

    if (!activeDelegation.IsActive
        || worker.ManagerId != 1
        || pendingLifecycleRequest.ManagerApproverEmployeeId != 1)
    {
        throw new InvalidOperationException(
            "HR approval did not activate delegation and reassign reports/pending approvals.");
    }

    await delegationService.ReconcileAsync(leaveEnd.AddDays(1));

    await database.Entry(worker).ReloadAsync();
    await database.Entry(pendingLifecycleRequest).ReloadAsync();
    await database.Entry(activeDelegation).ReloadAsync();
    if (activeDelegation.IsActive
        || worker.ManagerId != 2
        || pendingLifecycleRequest.ManagerApproverEmployeeId != 2)
    {
        throw new InvalidOperationException(
            "Expired delegation did not restore reports and pending approvals to the department manager.");
    }
    return;
}

await database.Database.EnsureDeletedAsync();
await database.Database.MigrateAsync();

var department = new Department
{
    DepartmentName = "E2E İnsan Kaynakları"
};
database.Departments.Add(department);
await database.SaveChangesAsync();

database.Employees.Add(new Employee
{
    SicilNo = "E2E-USER",
    FirstName = "E2E",
    LastName = "Kullanıcı",
    KktcKimlikNo = "1000000001",
    DepartmentId = department.DepartmentId,
    StartDate = new DateTime(2026, 1, 1),
    Status = EmploymentStatus.Active,
    CreatedAt = DateTimeOffset.UtcNow,
    UpdatedAt = DateTimeOffset.UtcNow
});
await database.SaveChangesAsync();

database.Employees.Add(new Employee
{
    SicilNo = "E2E-ADMIN",
    FirstName = "E2E",
    LastName = "Yönetici",
    KktcKimlikNo = "1000000002",
    DepartmentId = department.DepartmentId,
    StartDate = new DateTime(2026, 1, 1),
    Status = EmploymentStatus.Active,
    CreatedAt = DateTimeOffset.UtcNow,
    UpdatedAt = DateTimeOffset.UtcNow
});
await database.SaveChangesAsync();

database.Employees.Add(
    new Employee
    {
        SicilNo = "E2E-WORKER",
        FirstName = "E2E",
        LastName = "Çalışan",
        KktcKimlikNo = "1000000003",
        DepartmentId = department.DepartmentId,
        ManagerId = 2,
        StartDate = new DateTime(2026, 1, 1),
        Status = EmploymentStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    });
await database.SaveChangesAsync();

database.Employees.AddRange(Enumerable.Range(4, 30).Select(id => new Employee
{
    SicilNo = $"E2E-FILLER-{id}",
    FirstName = "E2E",
    LastName = $"Dolgu {id}",
    KktcKimlikNo = (2_000_000_000L + id).ToString(),
    DepartmentId = department.DepartmentId,
    ManagerId = 2,
    StartDate = new DateTime(2026, 1, 1),
    Status = EmploymentStatus.Active,
    CreatedAt = DateTimeOffset.UtcNow,
    UpdatedAt = DateTimeOffset.UtcNow
}));
await database.SaveChangesAsync();

database.Employees.Add(
    new Employee
    {
        SicilNo = "E2E-HR",
        FirstName = "E2E",
        LastName = "İnsan Kaynakları",
        KktcKimlikNo = "1000000034",
        DepartmentId = department.DepartmentId,
        ManagerId = 2,
        StartDate = new DateTime(2026, 1, 1),
        Status = EmploymentStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    });
await database.SaveChangesAsync();

if (await database.Employees.SingleAsync(employee => employee.SicilNo == "E2E-HR")
    is not { EmployeeId: 34 })
{
    throw new InvalidOperationException("E2E HR fixture must resolve to EmployeeId 34.");
}

if (await database.Employees.SingleAsync(employee => employee.SicilNo == "E2E-ADMIN")
    is not { EmployeeId: 2 })
{
    throw new InvalidOperationException("E2E admin fixture must resolve to EmployeeId 2.");
}

department.ManagerEmployeeId = 2;
var ordinaryEmployee = await database.Employees
    .SingleAsync(employee => employee.EmployeeId == 1);
ordinaryEmployee.ManagerId = 2;
await database.SaveChangesAsync();

var leaveType = new LeaveType
{
    Name = "E2E Yıllık İzin",
    AnnualQuota = 30
};
database.LeaveTypes.Add(leaveType);
await database.SaveChangesAsync();
database.LeaveBalances.Add(new LeaveBalance
{
    EmployeeId = 2,
    LeaveTypeId = leaveType.LeaveTypeId,
    Year = DateTime.Today.Year,
    EntitledDays = 30,
    UsedDays = 0,
    RemainingDays = 30
});
database.LeaveRequests.Add(new LeaveRequest
{
    EmployeeId = 3,
    LeaveTypeId = leaveType.LeaveTypeId,
    StartDate = DateTime.Today.AddDays(10),
    EndDate = DateTime.Today.AddDays(10),
    RequestedDays = 1,
    Reason = "E2E bekleyen yönetici onayı",
    CurrentStatus = LeaveRequestStatus.ManagerReview,
    ManagerApproverEmployeeId = 2,
    CreatedAt = DateTimeOffset.UtcNow,
    UpdatedAt = DateTimeOffset.UtcNow
});
await database.SaveChangesAsync();

var pendingRequest = await database.LeaveRequests.SingleAsync(request =>
    request.Reason == "E2E bekleyen yönetici onayı");
database.LeaveApprovals.Add(new LeaveApproval
{
    RequestId = pendingRequest.RequestId,
    ApproverRole = LeaveApproverRole.Manager,
    Decision = LeaveApprovalDecision.Pending,
    CreatedAt = DateTimeOffset.UtcNow
});
await database.SaveChangesAsync();

var setupImportPath = Environment.GetEnvironmentVariable("IK_E2E_IMPORT_PATH");
if (!string.IsNullOrWhiteSpace(setupImportPath))
{
    CreatePublicHolidayWorkbook(setupImportPath);
}

var employeeIdentity = new EmployeeIdentityDocument
{
    EmployeeId = 1,
    DocumentType = "E2E Sahiplik Kontrolü",
    DocumentNumber = "E2E-OWNERSHIP-PROBE"
};
database.EmployeeIdentityDocuments.Add(employeeIdentity);
await database.SaveChangesAsync();

try
{
    await database.Database.ExecuteSqlInterpolatedAsync(
        $"""
        INSERT INTO EmployeeDocuments
            (EmployeeId, CategoryCanonicalKey, EmployeeIdentityDocumentId,
             OriginalFileName, ContentType, StorageKey, SizeBytes, UploadedAt)
        VALUES
            (2, N'identity', {employeeIdentity.EmployeeIdentityDocumentId},
             N'ownership-probe.pdf', N'application/pdf',
             N'employees/0000000002/documents/identity/ownership-probe.pdf',
             1, SYSDATETIMEOFFSET())
        """);
    throw new InvalidOperationException(
        "Composite employee-document ownership foreign key accepted a cross-employee link.");
}
catch (SqlException exception) when (exception.Number == 547)
{
    // Expected: the real MSSQL composite FK rejects a cross-employee related record.
}

static async Task ValidateMigrationGuardsAsync(
    DbContextOptions<HumanResourcesDbContext> options)
{
    await AssertMigrationRejectedAsync(options, seedCycle: false, expectedErrorNumber: 51004);
    await AssertMigrationRejectedAsync(options, seedCycle: true, expectedErrorNumber: 51003);
}

static async Task AssertMigrationRejectedAsync(
    DbContextOptions<HumanResourcesDbContext> options,
    bool seedCycle,
    int expectedErrorNumber)
{
    await using var validationDatabase = new HumanResourcesDbContext(options);
    await validationDatabase.Database.EnsureDeletedAsync();
    try
    {
        var migrator = validationDatabase.GetService<IMigrator>();
        await migrator.MigrateAsync("20260728132817_AddDepartmentManagerDelegation");

        var parent = new Department { DepartmentName = seedCycle ? "Cycle A" : "Parent" };
        var child = new Department { DepartmentName = seedCycle ? "Cycle B" : "Child", ParentDepartment = parent };
        validationDatabase.Departments.AddRange(parent, child);
        await validationDatabase.SaveChangesAsync();

        if (seedCycle)
        {
            parent.ParentDepartmentId = child.DepartmentId;
        }
        else
        {
            var childManager = new Employee
            {
                FirstName = "Migration",
                LastName = "Manager",
                SicilNo = "MIGRATION-MANAGER",
                KktcKimlikNo = "9999999999",
                DepartmentId = child.DepartmentId,
                Status = EmploymentStatus.Active
            };
            validationDatabase.Employees.Add(childManager);
            await validationDatabase.SaveChangesAsync();
            child.ManagerEmployeeId = childManager.EmployeeId;
        }
        await validationDatabase.SaveChangesAsync();

        try
        {
            await migrator.MigrateAsync();
            throw new InvalidOperationException(
                $"Migration guard {expectedErrorNumber} did not reject invalid hierarchy.");
        }
        catch (SqlException exception) when (exception.Number == expectedErrorNumber)
        {
        }
    }
    finally
    {
        await validationDatabase.Database.EnsureDeletedAsync();
    }
}

static void CreatePublicHolidayWorkbook(string path)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
    var workbookPart = document.AddWorkbookPart();
    workbookPart.Workbook = new Workbook();
    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
    var sheetData = new SheetData();
    worksheetPart.Worksheet = new Worksheet(sheetData);
    sheetData.Append(
        Row("Tarih", "Tatil Adı"),
        Row($"{DateTime.Today.Year}-12-29", "E2E Toplu Aktarım Tatili"));
    var sheets = workbookPart.Workbook.AppendChild(new Sheets());
    sheets.Append(new Sheet
    {
        Id = workbookPart.GetIdOfPart(worksheetPart),
        SheetId = 1,
        Name = "Veriler"
    });
    workbookPart.Workbook.Save();

    static Row Row(params string[] values)
    {
        var row = new Row();
        foreach (var value in values)
        {
            row.Append(new Cell
            {
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value))
            });
        }
        return row;
    }
}
