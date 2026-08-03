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
if (mode is not ("validate" or "validate-migration-guards" or "validate-balance-backed-cutover" or "setup" or "teardown" or "verify-delegation-lifecycle"))
{
    throw new InvalidOperationException(
        "FixtureTool requires validate, validate-migration-guards, validate-balance-backed-cutover, setup, teardown or verify-delegation-lifecycle.");
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

if (mode == "validate-balance-backed-cutover")
{
    await ValidateBalanceBackedCutoverAsync(options);
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
        request.EmployeeId == 6
        && request.DelegateEmployeeId == 1
        && request.CurrentStatus == LeaveRequestStatus.Approved);
    var leaveStart = DateOnly.FromDateTime(managerLeave.StartDate!.Value);
    var leaveEnd = DateOnly.FromDateTime(managerLeave.EndDate!.Value);
    var delegationService = new ManagerDelegationService(
        database,
        new AuditLogService(database),
        new FixtureTimeProvider(leaveStart));
    await delegationService.ReconcileAsync(leaveStart);

    var activeDelegation = await database.ManagerDelegations.SingleAsync(item =>
        item.LeaveRequestId == managerLeave.RequestId);
    var pendingLifecycleRequest = await database.LeaveRequests.SingleAsync(request =>
        request.Reason == "E2E bekleyen yönetici onayı");
    var worker = await database.Employees.SingleAsync(employee => employee.SicilNo == "E2E-WORKER");

    var delegatedDepartment = await database.Departments.SingleAsync(item =>
        item.ManagerEmployeeId == managerLeave.EmployeeId);
    if (delegatedDepartment.ActiveDelegateEmployeeId != 1
        || activeDelegation.RestoredAt is not null
        || worker.ManagerId != 1
        || pendingLifecycleRequest.ManagerApproverEmployeeId != 1)
    {
        throw new InvalidOperationException(
            "HR approval did not activate delegation and reassign reports/pending approvals.");
    }

    await delegationService.TransferActiveDelegationAsync(
        delegatedDepartment.DepartmentId,
        actorEmployeeId: 1,
        newDelegateEmployeeId: 2,
        actorUserId: "e2e-delegate");
    await database.Entry(delegatedDepartment).ReloadAsync();
    await database.Entry(pendingLifecycleRequest).ReloadAsync();
    var manualTransfer = await database.ManagerDelegations.SingleAsync(item =>
        item.LeaveRequestId == null
        && item.ParentManagerDelegationId == activeDelegation.ManagerDelegationId);
    if (delegatedDepartment.ActiveDelegateEmployeeId != 2
        || manualTransfer.ManagerEmployeeId != 1
        || manualTransfer.DelegateEmployeeId != 2
        || pendingLifecycleRequest.ManagerApproverEmployeeId != 2)
    {
        throw new InvalidOperationException(
            "Active delegate could not manually transfer authority without taking leave.");
    }

    await delegationService.ReconcileAsync(leaveEnd.AddDays(1));

    await database.Entry(worker).ReloadAsync();
    await database.Entry(pendingLifecycleRequest).ReloadAsync();
    await database.Entry(activeDelegation).ReloadAsync();
    await database.Entry(delegatedDepartment).ReloadAsync();
    if (delegatedDepartment.ActiveDelegateEmployeeId is not null
        || activeDelegation.RestoredAt is null
        || worker.ManagerId != 6
        || pendingLifecycleRequest.ManagerApproverEmployeeId != 6)
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
    SicilNo = "E2E-WORKER",
    FirstName = "E2E",
    LastName = "Çalışan",
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
        SicilNo = "E2E-HR",
        FirstName = "E2E",
        LastName = "İnsan Kaynakları",
        KktcKimlikNo = "1000000003",
        DepartmentId = department.DepartmentId,
        StartDate = new DateTime(2026, 1, 1),
        Status = EmploymentStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    });
await database.SaveChangesAsync();

database.Employees.AddRange(Enumerable.Range(4, 2).Select(id => new Employee
{
    SicilNo = $"E2E-FILLER-{id}",
    FirstName = "E2E",
    LastName = $"Dolgu {id}",
    KktcKimlikNo = (2_000_000_000L + id).ToString(),
    DepartmentId = department.DepartmentId,
    StartDate = new DateTime(2026, 1, 1),
    Status = EmploymentStatus.Active,
    CreatedAt = DateTimeOffset.UtcNow,
    UpdatedAt = DateTimeOffset.UtcNow
}));
await database.SaveChangesAsync();

database.Employees.Add(
    new Employee
    {
        SicilNo = "E2E-ADMIN",
        FirstName = "E2E",
        LastName = "Yönetici",
        KktcKimlikNo = "1000000006",
        DepartmentId = department.DepartmentId,
        Gender = EmployeeGender.Male,
        StartDate = new DateTime(2026, 1, 1),
        Status = EmploymentStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    });
await database.SaveChangesAsync();

department.ManagerEmployeeId = 6;
var initialReports = await database.Employees
    .Where(employee => employee.EmployeeId != 6)
    .ToListAsync();
foreach (var report in initialReports)
{
    report.ManagerId = 6;
}
await database.SaveChangesAsync();

database.Employees.AddRange(Enumerable.Range(7, 28).Select(id => new Employee
{
    SicilNo = $"E2E-FILLER-{id}",
    FirstName = "E2E",
    LastName = $"Dolgu {id}",
    KktcKimlikNo = (2_000_000_000L + id).ToString(),
    DepartmentId = department.DepartmentId,
    ManagerId = 6,
    StartDate = new DateTime(2026, 1, 1),
    Status = EmploymentStatus.Active,
    CreatedAt = DateTimeOffset.UtcNow,
    UpdatedAt = DateTimeOffset.UtcNow
}));
await database.SaveChangesAsync();

if (await database.Employees.SingleAsync(employee => employee.SicilNo == "E2E-HR")
    is not { EmployeeId: 3 })
{
    throw new InvalidOperationException("E2E HR fixture must resolve to EmployeeId 3.");
}

if (await database.Employees.SingleAsync(employee => employee.SicilNo == "E2E-ADMIN")
    is not { EmployeeId: 6 })
{
    throw new InvalidOperationException("E2E admin fixture must resolve to EmployeeId 6.");
}

var leaveType = new LeaveType
{
    Name = "E2E Manuel İzin",
    AnnualQuota = 30
};
database.LeaveTypes.Add(leaveType);
await database.SaveChangesAsync();
database.LeaveBalances.Add(new LeaveBalance
{
    EmployeeId = 6,
    LeaveTypeId = leaveType.LeaveTypeId,
    Year = DateTime.Today.Year,
    EntitledDays = 30,
    UsedDays = 0,
    RemainingDays = 30
});
database.LeaveRequests.Add(new LeaveRequest
{
    EmployeeId = 2,
    Category = LeaveRequestCategory.SpecificLeaveType,
    LeaveTypeId = leaveType.LeaveTypeId,
    StartDate = DateTime.Today.AddDays(10),
    EndDate = DateTime.Today.AddDays(10),
    RequestedDays = 1,
    Reason = "E2E bekleyen yönetici onayı",
    CurrentStatus = LeaveRequestStatus.ManagerReview,
    ManagerApproverEmployeeId = 6,
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
    await AssertActiveDelegationMigrationRejectedAsync(options);
}

static async Task ValidateBalanceBackedCutoverAsync(
    DbContextOptions<HumanResourcesDbContext> options)
{
    await using var validationDatabase = new HumanResourcesDbContext(options);
    await validationDatabase.Database.EnsureDeletedAsync();
    try
    {
        var migrator = validationDatabase.GetService<IMigrator>();
        await migrator.MigrateAsync("20260803072742_DailyLeaveEntitlementsAndCategories");
        await validationDatabase.Database.ExecuteSqlRawAsync(
            """
            DECLARE @DepartmentId int;
            DECLARE @ManagerId int;
            DECLARE @DelegateId int;
            DECLARE @ReportId int;
            DECLARE @ManualLeaveTypeId int;
            DECLARE @RequestId int;

            INSERT INTO Departments (DepartmentName)
            VALUES (N'Cutover Korunan Departman');
            SET @DepartmentId = SCOPE_IDENTITY();

            INSERT INTO Employees
                (FirstName, LastName, SicilNo, KKTC_KimlikNo, DepartmentId,
                 Gender, Status, CreatedAt, UpdatedAt)
            VALUES
                (N'Cutover', N'Yönetici', N'CUTOVER-MANAGER', N'8777777771',
                 @DepartmentId, 2, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
            SET @ManagerId = SCOPE_IDENTITY();

            INSERT INTO Employees
                (FirstName, LastName, SicilNo, KKTC_KimlikNo, DepartmentId,
                 Gender, Status, CreatedAt, UpdatedAt)
            VALUES
                (N'Cutover', N'Vekil', N'CUTOVER-DELEGATE', N'8777777772',
                 @DepartmentId, 2, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
            SET @DelegateId = SCOPE_IDENTITY();

            INSERT INTO Employees
                (FirstName, LastName, SicilNo, KKTC_KimlikNo, DepartmentId,
                 ManagerId, Gender, Status, CreatedAt, UpdatedAt)
            VALUES
                (N'Cutover', N'Çalışan', N'CUTOVER-REPORT', N'8777777773',
                 @DepartmentId, @DelegateId, 2, 1,
                 SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
            SET @ReportId = SCOPE_IDENTITY();

            UPDATE Departments
            SET ManagerEmployeeId = @ManagerId,
                ActiveDelegateEmployeeId = @DelegateId
            WHERE DepartmentId = @DepartmentId;

            INSERT INTO LeaveTypes
                (Name, AnnualQuota, CarryOverRule, MaxAccrualDays, EntitlementKind)
            VALUES (N'ID 6 Manuel Tür', 5, 0, 5, 0);
            SET @ManualLeaveTypeId = SCOPE_IDENTITY();

            IF @ManualLeaveTypeId <> 6
                THROW 51006, 'Cutover fixture expected the first manual leave type to use ID 6.', 1;

            INSERT INTO LeaveBalances
                (EmployeeId, LeaveTypeId, [Year], EntitledDays, CarryOverDays,
                 UsedDays, RemainingDays, CarryOverLimitWarningConfirmed,
                 CreatedAt, UpdatedAt)
            VALUES
                (@ReportId, @ManualLeaveTypeId, YEAR(GETDATE()), 5, 0, 0, 5, 0,
                 SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

            INSERT INTO LeaveRequests
                (EmployeeId, Category, StartDate, EndDate, RequestedDays, Reason,
                 CurrentStatus, DelegateEmployeeId, CreatedAt, UpdatedAt)
            VALUES
                (@ManagerId, 2, CAST(GETDATE() AS date), CAST(GETDATE() AS date), 1,
                 N'Eski Hastalık kategorisi talebi', 3, @DelegateId,
                 SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
            SET @RequestId = SCOPE_IDENTITY();

            INSERT INTO ManagerDelegations
                (LeaveRequestId, DepartmentId, ManagerEmployeeId, DelegateEmployeeId,
                 StartDate, EndDate, ActivatedAt, RestoredAt)
            VALUES
                (@RequestId, @DepartmentId, @ManagerId, @DelegateId,
                 CAST(GETDATE() AS date), DATEADD(day, 1, CAST(GETDATE() AS date)),
                 SYSDATETIMEOFFSET(), NULL);
            """);

        await migrator.MigrateAsync();
        validationDatabase.ChangeTracker.Clear();

        var preservedDepartment = await validationDatabase.Departments.SingleAsync(
            item => item.DepartmentName == "Cutover Korunan Departman");
        var preservedManager = await validationDatabase.Employees.SingleAsync(
            item => item.SicilNo == "CUTOVER-MANAGER");
        var preservedDelegate = await validationDatabase.Employees.SingleAsync(
            item => item.SicilNo == "CUTOVER-DELEGATE");
        var preservedReport = await validationDatabase.Employees.SingleAsync(
            item => item.SicilNo == "CUTOVER-REPORT");
        if (preservedDepartment.ManagerEmployeeId != preservedManager.EmployeeId
            || preservedDepartment.ActiveDelegateEmployeeId is not null
            || preservedManager.ManagerId is not null
            || preservedDelegate.ManagerId != preservedManager.EmployeeId
            || preservedReport.ManagerId != preservedManager.EmployeeId
            || await validationDatabase.ManagerDelegations.AnyAsync()
            || await validationDatabase.LeaveRequests.AnyAsync()
            || await validationDatabase.LeaveBalances.AnyAsync())
        {
            throw new InvalidOperationException(
                "Development cutover did not preserve personnel and restore canonical manager authority while resetting obsolete leave rows.");
        }

        var leaveTypes = await validationDatabase.LeaveTypes
            .OrderBy(item => item.LeaveTypeId)
            .ToArrayAsync();
        if (leaveTypes.Length != 6
            || leaveTypes.Select(item => item.LeaveTypeId).SequenceEqual(Enumerable.Range(1, 6)) is false
            || leaveTypes[5].Name != "Seferberlik İzni"
            || leaveTypes[5].AnnualQuota != DomainConstants.MobilizationLeaveMaximumDays)
        {
            throw new InvalidOperationException(
                "Development cutover did not rebuild the single default leave-type authority at IDs 1-6.");
        }
    }
    finally
    {
        await validationDatabase.Database.EnsureDeletedAsync();
    }
}

static async Task AssertActiveDelegationMigrationRejectedAsync(
    DbContextOptions<HumanResourcesDbContext> options)
{
    await using var validationDatabase = new HumanResourcesDbContext(options);
    await validationDatabase.Database.EnsureDeletedAsync();
    try
    {
        var migrator = validationDatabase.GetService<IMigrator>();
        await migrator.MigrateAsync("20260728144637_BackfillDepartmentManagerAuthority");
        await validationDatabase.Database.ExecuteSqlRawAsync(
            """
            DECLARE @DepartmentId int;
            DECLARE @ManagerId int;
            DECLARE @DelegateId int;
            DECLARE @LeaveTypeId int;
            DECLARE @RequestId int;

            INSERT INTO Departments (DepartmentName)
            VALUES (N'Active Delegation Guard');
            SET @DepartmentId = SCOPE_IDENTITY();

            INSERT INTO Employees
                (FirstName, LastName, SicilNo, KKTC_KimlikNo, DepartmentId,
                 Status, CreatedAt, UpdatedAt)
            VALUES
                (N'Guard', N'Manager', N'GUARD-MANAGER', N'8888888881',
                 @DepartmentId, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
            SET @ManagerId = SCOPE_IDENTITY();

            INSERT INTO Employees
                (FirstName, LastName, SicilNo, KKTC_KimlikNo, DepartmentId,
                 ManagerId, Status, CreatedAt, UpdatedAt)
            VALUES
                (N'Guard', N'Delegate', N'GUARD-DELEGATE', N'8888888882',
                 @DepartmentId, @ManagerId, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
            SET @DelegateId = SCOPE_IDENTITY();

            UPDATE Departments
            SET ManagerEmployeeId = @ManagerId
            WHERE DepartmentId = @DepartmentId;

            INSERT INTO LeaveTypes (Name, AnnualQuota, CarryOverRule, MaxAccrualDays)
            VALUES (N'Guard Leave', 20, 0, 20);
            SET @LeaveTypeId = SCOPE_IDENTITY();

            INSERT INTO LeaveRequests
                (EmployeeId, LeaveTypeId, StartDate, EndDate, RequestedDays,
                 Reason, CurrentStatus, DelegateEmployeeId, CreatedAt, UpdatedAt)
            VALUES
                (@ManagerId, @LeaveTypeId, CAST(GETDATE() AS date),
                 DATEADD(day, 1, CAST(GETDATE() AS date)), 2,
                 N'Active migration guard', 3, @DelegateId,
                 SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
            SET @RequestId = SCOPE_IDENTITY();

            INSERT INTO ManagerDelegations
                (LeaveRequestId, DepartmentId, ManagerEmployeeId, DelegateEmployeeId,
                 StartDate, EndDate, IsActive, ActivatedAt, RestoredAt)
            VALUES
                (@RequestId, @DepartmentId, @ManagerId, @DelegateId,
                 CAST(GETDATE() AS date), DATEADD(day, 1, CAST(GETDATE() AS date)),
                 1, SYSDATETIMEOFFSET(), NULL);
            """);

        try
        {
            await migrator.MigrateAsync();
            throw new InvalidOperationException(
                "Migration guard 51005 did not reject an active legacy delegation.");
        }
        catch (SqlException exception) when (exception.Number == 51005)
        {
        }
    }
    finally
    {
        await validationDatabase.Database.EnsureDeletedAsync();
    }
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

        await validationDatabase.Database.OpenConnectionAsync();
        var connection = validationDatabase.Database.GetDbConnection();
        var parentId = await InsertDepartmentAsync(
            connection,
            seedCycle ? "Cycle A" : "Parent",
            parentDepartmentId: null);
        var childId = await InsertDepartmentAsync(
            connection,
            seedCycle ? "Cycle B" : "Child",
            parentId);

        if (seedCycle)
        {
            await validationDatabase.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Departments SET ParentDepartmentId = {childId} WHERE DepartmentId = {parentId}");
        }
        else
        {
            await using var employeeCommand = connection.CreateCommand();
            employeeCommand.CommandText =
                """
                INSERT INTO Employees
                    (FirstName, LastName, SicilNo, KKTC_KimlikNo, DepartmentId,
                     Status, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.EmployeeId
                VALUES
                    (N'Migration', N'Manager', N'MIGRATION-MANAGER', N'9999999999',
                     @departmentId, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())
                """;
            var departmentParameter = employeeCommand.CreateParameter();
            departmentParameter.ParameterName = "@departmentId";
            departmentParameter.Value = childId;
            employeeCommand.Parameters.Add(departmentParameter);
            var childManagerId = Convert.ToInt32(
                await employeeCommand.ExecuteScalarAsync());
            await validationDatabase.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Departments SET ManagerEmployeeId = {childManagerId} WHERE DepartmentId = {childId}");
        }

        static async Task<int> InsertDepartmentAsync(
            System.Data.Common.DbConnection connection,
            string name,
            int? parentDepartmentId)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO Departments (DepartmentName, ParentDepartmentId)
                OUTPUT INSERTED.DepartmentId
                VALUES (@name, @parentDepartmentId)
                """;
            var nameParameter = command.CreateParameter();
            nameParameter.ParameterName = "@name";
            nameParameter.Value = name;
            command.Parameters.Add(nameParameter);
            var parentParameter = command.CreateParameter();
            parentParameter.ParameterName = "@parentDepartmentId";
            parentParameter.Value = parentDepartmentId.HasValue
                ? parentDepartmentId.Value
                : DBNull.Value;
            command.Parameters.Add(parentParameter);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

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

sealed class FixtureTimeProvider(DateOnly today) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() =>
        new(today.Year, today.Month, today.Day, 12, 0, 0, TimeSpan.Zero);
}
