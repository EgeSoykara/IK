using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IK.Web.Tests;

public sealed class PersonnelInformationSchemaContractTests
{
    [Fact]
    public void PersonnelInformationEntities_AreRelationalAttributeMappedAndRestrictEmployeeDeletion()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaContract;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new HumanResourcesDbContext(options);
        var model = dbContext.GetService<IDesignTimeModel>().Model;

        var entityTypes = new[]
        {
            typeof(EmployeeBankAccount),
            typeof(EmployeeIdentityDocument),
            typeof(EmployeePhone),
            typeof(EmployeeAddress),
            typeof(EmployeeEducation),
            typeof(EmployeeCourseCertificate),
            typeof(EmployeeTermination)
        };

        foreach (var clrType in entityTypes)
        {
            var entityType = Assert.IsAssignableFrom<IEntityType>(model.FindEntityType(clrType));
            Assert.Contains(
                entityType.GetForeignKeys(),
                foreignKey =>
                    foreignKey.PrincipalEntityType.ClrType == typeof(Employee)
                    && foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
            Assert.Contains(
                entityType.GetIndexes(),
                index => index.Properties.Any(property => property.Name == nameof(EmployeeBankAccount.EmployeeId)));
        }
    }

    [Fact]
    public void PersonnelInformation_EnforcesExpectedUniquenessAndLengths()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaContract;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new HumanResourcesDbContext(options);
        var model = dbContext.GetService<IDesignTimeModel>().Model;

        var employee = model.FindEntityType(typeof(Employee))!;
        Assert.True(employee.FindProperty(nameof(Employee.StaffDate))!.IsNullable);
        Assert.Equal(
            "datetime2",
            employee.FindProperty(nameof(Employee.StaffDate))!.GetColumnType());
        Assert.True(employee.FindProperty(nameof(Employee.Gender))!.IsNullable);
        Assert.True(employee.FindProperty(nameof(Employee.BloodGroup))!.IsNullable);
        Assert.Contains(employee.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_Employees_Gender");
        Assert.Contains(employee.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_Employees_BloodGroup");
        var email = employee.FindProperty(nameof(Employee.Email))!;
        Assert.False(email.IsNullable);
        Assert.Equal(254, email.GetMaxLength());
        Assert.Contains(employee.GetIndexes(), index =>
            index.IsUnique
            && index.GetFilter() is null
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Employee.Email)]));
        Assert.Contains(employee.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(ApplicationRole)
            && foreignKey.DeleteBehavior == DeleteBehavior.Restrict
            && foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Employee.ApplicationRoleId)]));

        var rolePermission = model.FindEntityType(typeof(ApplicationRolePermission))!;
        Assert.Equal(
            [nameof(ApplicationRolePermission.ApplicationRoleId), nameof(ApplicationRolePermission.PermissionName)],
            rolePermission.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Contains(rolePermission.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(ApplicationRole)
            && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);

        var bank = model.FindEntityType(typeof(EmployeeBankAccount))!;
        Assert.Equal(34, bank.FindProperty(nameof(EmployeeBankAccount.Iban))!.GetMaxLength());
        Assert.Contains(bank.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(EmployeeBankAccount.Iban)]));

        var identity = model.FindEntityType(typeof(EmployeeIdentityDocument))!;
        Assert.Contains(identity.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                [
                    nameof(EmployeeIdentityDocument.EmployeeId),
                    nameof(EmployeeIdentityDocument.DocumentType),
                    nameof(EmployeeIdentityDocument.DocumentNumber)
                ]));

        var education = model.FindEntityType(typeof(EmployeeEducation))!;
        Assert.Equal(
            80,
            education.FindProperty(nameof(EmployeeEducation.EducationLevel))!.GetMaxLength());

        var termination = model.FindEntityType(typeof(EmployeeTermination))!;
        Assert.Contains(termination.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(EmployeeTermination.EmployeeId)]));

        AssertFilteredPrimaryIndex(bank);
        AssertFilteredPrimaryIndex(model.FindEntityType(typeof(EmployeePhone))!);
        AssertFilteredPrimaryIndex(model.FindEntityType(typeof(EmployeeAddress))!);

        var document = model.FindEntityType(typeof(EmployeeDocument))!;
        foreach (var (relatedType, relatedId) in new[]
                 {
                     (typeof(EmployeeIdentityDocument), nameof(EmployeeDocument.EmployeeIdentityDocumentId)),
                     (typeof(EmployeeEducation), nameof(EmployeeDocument.EmployeeEducationId)),
                     (typeof(EmployeeCourseCertificate), nameof(EmployeeDocument.EmployeeCourseCertificateId))
                 })
        {
            Assert.Contains(document.GetForeignKeys(), foreignKey =>
                foreignKey.PrincipalEntityType.ClrType == relatedType
                && foreignKey.DeleteBehavior == DeleteBehavior.Restrict
                && foreignKey.Properties.Select(property => property.Name).SequenceEqual(
                    [nameof(EmployeeDocument.EmployeeId), relatedId])
                && foreignKey.PrincipalKey.Properties.Select(property => property.Name).SequenceEqual(
                    [nameof(EmployeeDocument.EmployeeId), relatedId]));
        }
    }

    [Fact]
    public void PersonnelInformationMigration_IsDiscoverableAndSnapshotIsCurrent()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaContract;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new HumanResourcesDbContext(options);
        var migrations = dbContext.GetService<IMigrationsAssembly>().Migrations;

        Assert.Contains("20260727000000_AddPersonnelInformation", migrations.Keys);
        Assert.Contains("20260728093000_AddEmployeeEducationLevel", migrations.Keys);
        Assert.Contains("20260728100000_AddEmployeeGenderAndBloodGroup", migrations.Keys);
        Assert.Contains("20260728103000_AddEmployeeStaffDate", migrations.Keys);
        Assert.Contains(
            migrations.Keys,
            migration => migration.EndsWith(
                "_PersistApplicationRolesAndEmployeeEmail",
                StringComparison.Ordinal));
        Assert.Contains(
            migrations.Keys,
            migration => migration.EndsWith(
                "_SynchronizeManagerResponsibilityRoles",
                StringComparison.Ordinal));
        Assert.Contains(
            migrations.Keys,
            migration => migration.EndsWith(
                "_LinkEmployeeDocumentsToPersonnelRecords",
                StringComparison.Ordinal));
        Assert.Contains(
            migrations.Keys,
            migration => migration.EndsWith(
                "_EnforceEmployeeDocumentRelatedOwnership",
                StringComparison.Ordinal));

        var migrationSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Migrations",
            migrations.Keys.Single(key => key.EndsWith(
                "_EnforceEmployeeDocumentRelatedOwnership",
                StringComparison.Ordinal)) + ".cs"));
        Assert.Contains("ownership mismatch detected", migrationSource);
        Assert.Contains("principalColumns: new[] { \"EmployeeId\"", migrationSource);

        var repositoryRoot = FindRepositoryRoot();
        var initialPersonnelMigration = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Migrations",
            "20260727000000_AddPersonnelInformation.cs"));
        var educationLevelMigration = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Migrations",
            "20260728093000_AddEmployeeEducationLevel.cs"));

        Assert.DoesNotContain("EducationLevel", initialPersonnelMigration);
        Assert.Contains("AddColumn<string>", educationLevelMigration);
        Assert.Contains("name: \"EducationLevel\"", educationLevelMigration);
        Assert.Contains("table: \"EmployeeEducations\"", educationLevelMigration);

        var employeeDemographicsMigration = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Migrations",
            "20260728100000_AddEmployeeGenderAndBloodGroup.cs"));
        Assert.Contains("name: \"Gender\"", employeeDemographicsMigration);
        Assert.Contains("name: \"BloodGroup\"", employeeDemographicsMigration);
        Assert.Contains("CK_Employees_Gender", employeeDemographicsMigration);
        Assert.Contains("CK_Employees_BloodGroup", employeeDemographicsMigration);

        var employeeStaffDateMigration = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Migrations",
            "20260728103000_AddEmployeeStaffDate.cs"));
        Assert.Contains("name: \"StaffDate\"", employeeStaffDateMigration);
        Assert.Contains("table: \"Employees\"", employeeStaffDateMigration);
        Assert.Contains("type: \"datetime2\"", employeeStaffDateMigration);
        Assert.Contains("nullable: true", employeeStaffDateMigration);

        var roleMigrationName = migrations.Keys.Single(key => key.EndsWith(
            "_PersistApplicationRolesAndEmployeeEmail",
            StringComparison.Ordinal));
        var roleMigration = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Migrations",
            roleMigrationName + ".cs"));
        Assert.Contains("name: \"ApplicationRoles\"", roleMigration);
        Assert.Contains("name: \"ApplicationRolePermissions\"", roleMigration);
        Assert.Contains("name: \"Email\"", roleMigration);
        Assert.Contains("CanActAsHumanResources", roleMigration);
        Assert.Contains("WHEN [EmployeeId] = 1 THEN 2", roleMigration);
        Assert.Contains("WHEN [EmployeeId] = 1002 THEN 3", roleMigration);

        var managerRoleMigrationName = migrations.Keys.Single(key => key.EndsWith(
            "_SynchronizeManagerResponsibilityRoles",
            StringComparison.Ordinal));
        var managerRoleMigration = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Migrations",
            managerRoleMigrationName + ".cs"));
        Assert.Contains("employee.[ApplicationRoleId] = 1", managerRoleMigration);
        Assert.Contains("department.[ManagerEmployeeId]", managerRoleMigration);
        Assert.Contains("department.[ActiveDelegateEmployeeId]", managerRoleMigration);
        Assert.Contains("N'manager-role-backfill'", managerRoleMigration);
        Assert.Contains("THROW 51070", managerRoleMigration);
    }

    private static void AssertFilteredPrimaryIndex(IEntityType entityType)
    {
        Assert.Contains(entityType.GetIndexes(), candidate =>
            !candidate.IsUnique
            && candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(EmployeeBankAccount.EmployeeId)]));

        var index = Assert.Single(entityType.GetIndexes(), candidate =>
            candidate.IsUnique
            && candidate.Properties.Select(property => property.Name)
                .SequenceEqual(
                [
                    nameof(EmployeeBankAccount.EmployeeId),
                    nameof(EmployeeBankAccount.IsPrimary)
                ])
            && candidate.GetFilter() == "[IsPrimary] = 1");

        Assert.StartsWith("UX_", index.GetDatabaseName());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "IK.Web.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root could not be located.");
    }
}
