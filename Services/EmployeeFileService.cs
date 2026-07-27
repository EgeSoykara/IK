using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IK.Web.Services;

public sealed class EmployeeFileService(
    IDbContextFactory<HumanResourcesDbContext> dbContextFactory,
    IEmployeeFileStore fileStore,
    PageAccessService pageAccessService,
    ILogger<EmployeeFileService> logger)
{
    public async Task<IReadOnlyList<EmployeeDocument>> GetDocumentsAsync(
        ClaimsPrincipal principal,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManageEmployee(principal, employeeId);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
            cancellationToken);
        return await dbContext.EmployeeDocuments
            .AsNoTracking()
            .Include(document => document.Category)
            .Where(document => document.EmployeeId == employeeId)
            .OrderBy(document => document.Category.SortOrder)
            .ThenBy(document => document.Category.DisplayName)
            .ThenByDescending(document => document.UploadedAt)
            .ThenByDescending(document => document.EmployeeDocumentId)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeProfilePhoto> UploadProfilePhotoAsync(
        ClaimsPrincipal principal,
        int employeeId,
        EmployeeFileUpload upload,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManageEmployee(principal, employeeId);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
            cancellationToken);
        await EnsureEmployeeExistsAsync(dbContext, employeeId, cancellationToken);

        var validated = await EmployeeFileContentPolicy.ValidateProfilePhotoAsync(
            upload,
            cancellationToken);
        var existingPhoto = await dbContext.EmployeeProfilePhotos
            .SingleOrDefaultAsync(photo => photo.EmployeeId == employeeId, cancellationToken);
        var previousStorageKey = existingPhoto?.StorageKey;
        var storageKey = EmployeeFileCanonicalKey.CreateProfilePhoto(
            employeeId,
            Guid.NewGuid(),
            validated.Extension);

        await using var content = new MemoryStream(validated.Content, writable: false);
        await fileStore.SaveAsync(storageKey, content, cancellationToken);

        var profilePhoto = existingPhoto ?? new EmployeeProfilePhoto
        {
            EmployeeId = employeeId
        };

        profilePhoto.ContentType = validated.ContentType;
        profilePhoto.StorageKey = storageKey;
        profilePhoto.SizeBytes = validated.Content.LongLength;
        profilePhoto.UploadedAt = DateTimeOffset.UtcNow;

        if (existingPhoto is null)
        {
            dbContext.EmployeeProfilePhotos.Add(profilePhoto);
        }

        var auditLogService = new AuditLogService(dbContext);
        await auditLogService.AppendAsync(
            AuditActionType.ProfilePhotoUploaded,
            nameof(EmployeeProfilePhoto),
            employeeId.ToString(),
            ResolveActorUserId(principal, employeeId),
            $"SizeBytes={profilePhoto.SizeBytes}",
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception operationException)
        {
            await TryDeleteFailedUploadAsync(storageKey, operationException);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(previousStorageKey)
            && !string.Equals(previousStorageKey, storageKey, StringComparison.Ordinal))
        {
            try
            {
                await fileStore.DeleteIfExistsAsync(
                    previousStorageKey,
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Previous profile photo could not be removed. EmployeeId={EmployeeId};StorageKey={StorageKey}",
                    employeeId,
                    previousStorageKey);
            }
        }

        return profilePhoto;
    }

    public Task<EmployeeDocument> UploadIdentityDocumentAsync(
        ClaimsPrincipal principal,
        int employeeId,
        int identityDocumentId,
        EmployeeFileUpload upload,
        CancellationToken cancellationToken = default) =>
        UploadDocumentAsync(
            principal,
            employeeId,
            EmployeeDocumentCategories.Identity,
            identityDocumentId,
            null,
            null,
            upload,
            cancellationToken);

    public Task<EmployeeDocument> UploadEducationDocumentAsync(
        ClaimsPrincipal principal,
        int employeeId,
        int educationId,
        EmployeeFileUpload upload,
        CancellationToken cancellationToken = default) =>
        UploadDocumentAsync(
            principal,
            employeeId,
            EmployeeDocumentCategories.Education,
            null,
            educationId,
            null,
            upload,
            cancellationToken);

    public Task<EmployeeDocument> UploadCourseCertificateDocumentAsync(
        ClaimsPrincipal principal,
        int employeeId,
        int courseCertificateId,
        EmployeeFileUpload upload,
        CancellationToken cancellationToken = default) =>
        UploadDocumentAsync(
            principal,
            employeeId,
            EmployeeDocumentCategories.Education,
            null,
            null,
            courseCertificateId,
            upload,
            cancellationToken);

    private async Task<EmployeeDocument> UploadDocumentAsync(
        ClaimsPrincipal principal,
        int employeeId,
        string categoryCanonicalKey,
        int? identityDocumentId,
        int? educationId,
        int? courseCertificateId,
        EmployeeFileUpload upload,
        CancellationToken cancellationToken)
    {
        EnsureCanManageEmployee(principal, employeeId);
        EmployeeFileCanonicalKey.ValidateCategory(categoryCanonicalKey);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
            cancellationToken);
        await EnsureEmployeeExistsAsync(dbContext, employeeId, cancellationToken);
        await EnsureRelatedRecordExistsAsync(
            dbContext,
            employeeId,
            identityDocumentId,
            educationId,
            courseCertificateId,
            cancellationToken);

        var categoryExists = await dbContext.EmployeeDocumentCategories
            .AnyAsync(
                category =>
                    category.CanonicalKey == categoryCanonicalKey
                    && category.IsActive,
                cancellationToken);
        if (!categoryExists)
        {
            throw new EmployeeFileValidationException(
                "Seçilen belge kategorisi aktif değil veya bulunamadı.");
        }

        var validated = await EmployeeFileContentPolicy.ValidateDocumentAsync(
            upload,
            cancellationToken);
        var storageKey = EmployeeFileCanonicalKey.CreateDocument(
            employeeId,
            categoryCanonicalKey,
            Guid.NewGuid(),
            validated.Extension);

        await using var content = new MemoryStream(validated.Content, writable: false);
        await fileStore.SaveAsync(storageKey, content, cancellationToken);

        var document = new EmployeeDocument
        {
            EmployeeId = employeeId,
            CategoryCanonicalKey = categoryCanonicalKey,
            EmployeeIdentityDocumentId = identityDocumentId,
            EmployeeEducationId = educationId,
            EmployeeCourseCertificateId = courseCertificateId,
            OriginalFileName = validated.OriginalFileName,
            ContentType = validated.ContentType,
            StorageKey = storageKey,
            SizeBytes = validated.Content.LongLength,
            UploadedAt = DateTimeOffset.UtcNow
        };
        IDbContextTransaction? transaction = null;

        try
        {
            if (dbContext.Database.IsRelational())
            {
                transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            }

            dbContext.EmployeeDocuments.Add(document);
            await dbContext.SaveChangesAsync(cancellationToken);

            var auditLogService = new AuditLogService(dbContext);
            await auditLogService.AppendAsync(
                AuditActionType.EmployeeDocumentUploaded,
                nameof(EmployeeDocument),
                document.EmployeeDocumentId.ToString(),
                ResolveActorUserId(principal, employeeId),
                $"CategoryCanonicalKey={categoryCanonicalKey};SizeBytes={document.SizeBytes}",
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception operationException)
        {
            if (transaction is not null)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    logger.LogError(
                        rollbackException,
                        "Employee document transaction rollback failed. StorageKey={StorageKey}",
                        storageKey);
                }
            }

            await TryDeleteFailedUploadAsync(storageKey, operationException);
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        return document;
    }

    public async Task<EmployeeFileDownload?> OpenProfilePhotoAsync(
        ClaimsPrincipal principal,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        if (!pageAccessService.CanEditPersonnelInformation(principal, employeeId))
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
            cancellationToken);
        var profilePhoto = await dbContext.EmployeeProfilePhotos
            .AsNoTracking()
            .SingleOrDefaultAsync(photo => photo.EmployeeId == employeeId, cancellationToken);
        if (profilePhoto is null)
        {
            return null;
        }

        var content = await fileStore.OpenReadAsync(
            profilePhoto.StorageKey,
            cancellationToken);
        return content is null
            ? null
            : new EmployeeFileDownload(content, profilePhoto.ContentType, null);
    }

    public async Task<EmployeeFileDownload?> OpenDocumentAsync(
        ClaimsPrincipal principal,
        long documentId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
            cancellationToken);
        var document = await dbContext.EmployeeDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.EmployeeDocumentId == documentId, cancellationToken);
        if (document is null
            || !pageAccessService.CanEditPersonnelInformation(principal, document.EmployeeId))
        {
            return null;
        }

        var content = await fileStore.OpenReadAsync(document.StorageKey, cancellationToken);
        return content is null
            ? null
            : new EmployeeFileDownload(
                content,
                document.ContentType,
                document.OriginalFileName);
    }

    private static async Task EnsureEmployeeExistsAsync(
        HumanResourcesDbContext dbContext,
        int employeeId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Employees.AnyAsync(
                employee => employee.EmployeeId == employeeId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Oturumdaki çalışan kaydı bulunamadı.");
        }
    }

    private static async Task EnsureRelatedRecordExistsAsync(
        HumanResourcesDbContext dbContext,
        int employeeId,
        int? identityDocumentId,
        int? educationId,
        int? courseCertificateId,
        CancellationToken cancellationToken)
    {
        var relatedRecordCount = Convert.ToInt32(identityDocumentId.HasValue)
            + Convert.ToInt32(educationId.HasValue)
            + Convert.ToInt32(courseCertificateId.HasValue);
        if (relatedRecordCount != 1)
        {
            throw new EmployeeFileValidationException(
                "Belge tam olarak bir personel kaydıyla ilişkilendirilmelidir.");
        }

        var relatedRecordExists = identityDocumentId.HasValue
            ? await dbContext.EmployeeIdentityDocuments.AnyAsync(
                record =>
                    record.EmployeeIdentityDocumentId == identityDocumentId.Value
                    && record.EmployeeId == employeeId,
                cancellationToken)
            : educationId.HasValue
                ? await dbContext.EmployeeEducations.AnyAsync(
                    record =>
                        record.EmployeeEducationId == educationId.Value
                        && record.EmployeeId == employeeId,
                    cancellationToken)
                : await dbContext.EmployeeCourseCertificates.AnyAsync(
                    record =>
                        record.EmployeeCourseCertificateId == courseCertificateId!.Value
                        && record.EmployeeId == employeeId,
                    cancellationToken);

        if (!relatedRecordExists)
        {
            throw new EmployeeFileValidationException(
                "Belgenin bağlanacağı personel kaydı bulunamadı.");
        }
    }

    private void EnsureCanManageEmployee(ClaimsPrincipal principal, int employeeId)
    {
        if (!pageAccessService.CanEditPersonnelInformation(principal, employeeId))
        {
            throw new UnauthorizedAccessException(
                "Bu çalışanın dosyalarına erişim yetkiniz yok.");
        }
    }

    private static string ResolveActorUserId(
        ClaimsPrincipal principal,
        int employeeId)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.Identity?.Name
            ?? employeeId.ToString();
    }

    private async Task TryDeleteFailedUploadAsync(
        string storageKey,
        Exception operationException)
    {
        try
        {
            await fileStore.DeleteIfExistsAsync(
                storageKey,
                CancellationToken.None);
        }
        catch (Exception cleanupException)
        {
            logger.LogError(
                cleanupException,
                "Failed employee upload cleanup could not remove the canonical file. StorageKey={StorageKey};OperationException={OperationException}",
                storageKey,
                operationException.GetType().Name);
        }
    }
}
