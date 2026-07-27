using IK.Web.Database;
using IK.Web.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var mode = args.SingleOrDefault();
if (mode is not ("validate" or "setup" or "teardown"))
{
    throw new InvalidOperationException("FixtureTool requires validate, setup or teardown.");
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

if (mode == "teardown")
{
    await database.Database.EnsureDeletedAsync();
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

if (await database.Employees.SingleAsync(employee => employee.SicilNo == "E2E-ADMIN")
    is not { EmployeeId: 2 })
{
    throw new InvalidOperationException("E2E admin fixture must resolve to EmployeeId 2.");
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
