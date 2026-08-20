using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Tests;

public sealed class PersonnelAuthorizationServiceTests
{
    [Fact]
    public async Task DepartmentManager_CanViewOnlyOwnDepartmentWithoutSensitiveEditOrDownload()
    {
        await using var db = await CreateDatabaseAsync();
        var service = CreateService(db);

        var managed = await service.ResolveAsync(Principal(1), 3);
        var outside = await service.ResolveAsync(Principal(1), 5);

        Assert.True(managed.CanView);
        Assert.False(managed.CanEdit);
        Assert.False(managed.CanViewSensitive);
        Assert.False(managed.CanEditSensitive);
        Assert.True(managed.CanPreviewDocument(EmployeeDocumentCategories.Education));
        Assert.False(managed.CanPreviewDocument(EmployeeDocumentCategories.Identity));
        Assert.False(managed.CanDownloadDocuments);
        Assert.False(outside.CanView);
    }

    [Fact]
    public async Task ActiveDelegate_LosesDepartmentScopeImmediatelyWhenDelegationEnds()
    {
        await using var db = await CreateDatabaseAsync();
        var service = CreateService(db);

        Assert.True((await service.ResolveAsync(Principal(2), 3)).CanView);

        var department = await db.Departments.SingleAsync(item => item.DepartmentId == 10);
        department.ActiveDelegateEmployeeId = null;
        await db.SaveChangesAsync();

        Assert.False((await service.ResolveAsync(Principal(2), 3)).CanView);
    }

    [Fact]
    public async Task GlobalPersonnelPermissions_ControlEditSensitiveAndDownloadIndependently()
    {
        await using var db = await CreateDatabaseAsync();
        var service = CreateService(db);
        var full = Principal(
            6,
            PermissionNames.CanViewAllPersonnelInformation,
            PermissionNames.CanEditAllPersonnelInformation,
            PermissionNames.CanAccessSensitivePersonnelInformation,
            PermissionNames.CanDownloadPersonnelDocuments);
        var readOnly = Principal(6, PermissionNames.CanViewAllPersonnelInformation);

        var fullAccess = await service.ResolveAsync(full, 5);
        var readOnlyAccess = await service.ResolveAsync(readOnly, 5);

        Assert.True(fullAccess.CanView);
        Assert.True(fullAccess.CanEdit);
        Assert.True(fullAccess.CanEditSensitive);
        Assert.True(fullAccess.CanDownloadDocuments);
        Assert.True(readOnlyAccess.CanView);
        Assert.False(readOnlyAccess.CanEdit);
        Assert.False(readOnlyAccess.CanViewSensitive);
        Assert.False(readOnlyAccess.CanDownloadDocuments);
    }

    [Fact]
    public async Task OwnEmployee_HasFullAccessWithoutElevatedPermission()
    {
        await using var db = await CreateDatabaseAsync();
        var access = await CreateService(db).ResolveAsync(Principal(3), 3);

        Assert.True(access.CanView);
        Assert.True(access.CanEdit);
        Assert.True(access.CanEditSensitive);
        Assert.True(access.CanPreviewDocument(EmployeeDocumentCategories.Identity));
        Assert.True(access.CanDownloadDocuments);
    }

    [Fact]
    public async Task VisibleEmployeeQuery_IsBoundedToOwnAndResponsibleDepartments()
    {
        await using var db = await CreateDatabaseAsync();
        var service = CreateService(db);

        var managerIds = await service.ApplyVisibleEmployees(Principal(1), db)
            .OrderBy(item => item.EmployeeId)
            .Select(item => item.EmployeeId)
            .ToListAsync();
        var delegateIds = await service.ApplyVisibleEmployees(Principal(2), db)
            .OrderBy(item => item.EmployeeId)
            .Select(item => item.EmployeeId)
            .ToListAsync();

        Assert.Equal(new[] { 1, 2, 3 }, managerIds);
        Assert.Equal(new[] { 1, 2, 3 }, delegateIds);
        Assert.True(await service.CanSelectManagedEmployeesAsync(Principal(1)));
        Assert.False(await service.CanSelectManagedEmployeesAsync(Principal(3)));
    }

    private static PersonnelAuthorizationService CreateService(HumanResourcesDbContext db)
    {
        var factory = TestHumanResourcesDbContextFactory.From(db);
        return new PersonnelAuthorizationService(factory, new PageAccessService(factory));
    }

    private static async Task<HumanResourcesDbContext> CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new HumanResourcesDbContext(options);
        db.Departments.AddRange(
            new Department
            {
                DepartmentId = 10,
                DepartmentName = "Operasyon",
                ManagerEmployeeId = 1,
                ActiveDelegateEmployeeId = 2
            },
            new Department
            {
                DepartmentId = 20,
                DepartmentName = "Finans",
                ManagerEmployeeId = 4
            });
        db.Employees.AddRange(
            Employee(1, 10),
            Employee(2, 10),
            Employee(3, 10),
            Employee(4, 20),
            Employee(5, 20),
            Employee(6, 20));
        await db.SaveChangesAsync();
        return db;
    }

    private static Employee Employee(int id, int departmentId) => new()
    {
        EmployeeId = id,
        ApplicationRoleId = ApplicationRoleDefaults.EmployeeRoleId,
        DepartmentId = departmentId,
        SicilNo = $"P{id:D4}",
        FirstName = "Çalışan",
        LastName = id.ToString(),
        KktcKimlikNo = id.ToString("D10"),
        Status = EmploymentStatus.Active
    };

    private static ClaimsPrincipal Principal(int employeeId, params string[] permissions) =>
        new(new ClaimsIdentity(
            permissions
                .Select(permission => new Claim(PermissionClaimTypes.Permission, permission))
                .Prepend(new Claim(UserClaimTypes.EmployeeId, employeeId.ToString()))
                .Append(new Claim(UserClaimTypes.MustChangePassword, bool.FalseString)),
            authenticationType: "test"));
}
