using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IK.Web.Tests;

public sealed class EmployeeFileSchemaContractTests
{
    [Fact]
    public void EmployeeDocuments_UseCanonicalCategoryKeyAndUniqueStorageKeys()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaContract;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new HumanResourcesDbContext(options);
        var designTimeModel = dbContext.GetService<IDesignTimeModel>().Model;

        var categoryType = designTimeModel.FindEntityType(typeof(EmployeeDocumentCategory));
        var documentType = designTimeModel.FindEntityType(typeof(EmployeeDocument));
        var profilePhotoType = designTimeModel.FindEntityType(typeof(EmployeeProfilePhoto));
        var auditLogType = designTimeModel.FindEntityType(typeof(AuditLog));

        Assert.NotNull(categoryType);
        Assert.NotNull(documentType);
        Assert.NotNull(profilePhotoType);
        Assert.NotNull(auditLogType);
        Assert.Equal(
            nameof(EmployeeDocumentCategory.CanonicalKey),
            Assert.Single(categoryType.FindPrimaryKey()!.Properties).Name);
        Assert.Contains(
            documentType.GetForeignKeys(),
            foreignKey =>
                foreignKey.PrincipalEntityType.ClrType == typeof(EmployeeDocumentCategory)
                && Assert.Single(foreignKey.Properties).Name
                    == nameof(EmployeeDocument.CategoryCanonicalKey));
        Assert.Contains(
            documentType.GetForeignKeys(),
            foreignKey =>
                foreignKey.PrincipalEntityType.ClrType == typeof(Employee)
                && foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(
            profilePhotoType.GetForeignKeys(),
            foreignKey =>
                foreignKey.PrincipalEntityType.ClrType == typeof(Employee)
                && foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(
            documentType.GetIndexes(),
            index =>
                index.IsUnique
                && Assert.Single(index.Properties).Name
                    == nameof(EmployeeDocument.StorageKey));
        Assert.Contains(
            profilePhotoType.GetIndexes(),
            index =>
                index.IsUnique
                && Assert.Single(index.Properties).Name
                    == nameof(EmployeeProfilePhoto.StorageKey));
        Assert.True(
            profilePhotoType.FindProperty(nameof(EmployeeProfilePhoto.RowVersion))
                ?.IsConcurrencyToken);
        Assert.Contains(
            categoryType.GetCheckConstraints(),
            checkConstraint =>
                checkConstraint.Name == "CK_EmployeeDocumentCategories_CanonicalKey"
                && checkConstraint.Sql.Contains("[CanonicalKey] <> ''", StringComparison.Ordinal));
        Assert.Contains(
            auditLogType.GetCheckConstraints(),
            checkConstraint =>
                checkConstraint.Name == "CK_AuditLogs_ActionType"
                && checkConstraint.Sql == "[ActionType] BETWEEN 1 AND 24");
    }
}
