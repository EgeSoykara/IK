using System.Security.Claims;
using System.Text;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IK.Web.Tests;

public sealed class EmployeeFileServiceTests
{
    [Fact]
    public async Task UploadEducationDocumentAsync_PersistsMetadataAndRelatedRecord()
    {
        await using var fixture = await EmployeeFileFixture.CreateAsync();
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nemployee document");

        var document = await fixture.Service.UploadEducationDocumentAsync(
            fixture.EmployeePrincipal,
            1,
            1,
            new EmployeeFileUpload(
                new MemoryStream(bytes, writable: false),
                "../diploma.pdf",
                bytes.LongLength));

        Assert.True(document.EmployeeDocumentId > 0);
        Assert.Equal("diploma.pdf", document.OriginalFileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(EmployeeDocumentCategories.Education, document.CategoryCanonicalKey);
        Assert.Equal(1, document.EmployeeEducationId);
        Assert.Matches(
            "^employees/0000000001/documents/education/[0-9a-f]{32}\\.pdf$",
            document.StorageKey);
        Assert.Equal(
            bytes,
            await File.ReadAllBytesAsync(fixture.ResolveStoragePath(document.StorageKey)));

        var documents = await fixture.Service.GetDocumentsAsync(
            fixture.EmployeePrincipal,
            1);
        var stored = Assert.Single(documents);
        Assert.Equal(document.EmployeeDocumentId, stored.EmployeeDocumentId);
        Assert.Equal("Eğitim ve Sertifika Belgeleri", stored.Category.DisplayName);

        var audit = Assert.Single(
            fixture.Database.AuditLogs.Where(log =>
                log.ActionType == AuditActionType.EmployeeDocumentUploaded));
        Assert.Equal(document.EmployeeDocumentId.ToString(), audit.EntityId);
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_ReplacesMetadataAndRemovesPreviousFile()
    {
        await using var fixture = await EmployeeFileFixture.CreateAsync();
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x01];

        var firstPhoto = await fixture.Service.UploadProfilePhotoAsync(
            fixture.EmployeePrincipal,
            1,
            new EmployeeFileUpload(
                new MemoryStream(pngBytes, writable: false),
                "photo.png",
                pngBytes.LongLength));
        var firstStorageKey = firstPhoto.StorageKey;
        var firstPath = fixture.ResolveStoragePath(firstStorageKey);

        var secondPhoto = await fixture.Service.UploadProfilePhotoAsync(
            fixture.EmployeePrincipal,
            1,
            new EmployeeFileUpload(
                new MemoryStream(jpegBytes, writable: false),
                "photo.jpg",
                jpegBytes.LongLength));

        Assert.Equal(firstPhoto.EmployeeId, secondPhoto.EmployeeId);
        Assert.NotEqual(firstStorageKey, secondPhoto.StorageKey);
        Assert.False(File.Exists(firstPath));
        Assert.True(File.Exists(fixture.ResolveStoragePath(secondPhoto.StorageKey)));
        Assert.Equal("image/jpeg", secondPhoto.ContentType);
        Assert.Single(fixture.Database.EmployeeProfilePhotos);
        Assert.Equal(
            2,
            fixture.Database.AuditLogs.Count(log =>
                log.ActionType == AuditActionType.ProfilePhotoUploaded));
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_ConcurrencyLossRemovesNewFileAndKeepsPreviousPhoto()
    {
        var interceptor = new ControlledSaveChangesInterceptor();
        await using var fixture = await EmployeeFileFixture.CreateAsync(interceptor);
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x01];
        var firstPhoto = await fixture.Service.UploadProfilePhotoAsync(
            fixture.EmployeePrincipal,
            1,
            new EmployeeFileUpload(
                new MemoryStream(pngBytes, writable: false),
                "photo.png",
                pngBytes.LongLength));

        interceptor.FailNext(
            () => new DbUpdateConcurrencyException("Simulated profile photo race."));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => fixture.Service.UploadProfilePhotoAsync(
                fixture.EmployeePrincipal,
                1,
                new EmployeeFileUpload(
                    new MemoryStream(jpegBytes, writable: false),
                    "photo.jpg",
                    jpegBytes.LongLength)));

        var storedPhoto = await fixture.Database.EmployeeProfilePhotos
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(firstPhoto.StorageKey, storedPhoto.StorageKey);
        Assert.True(File.Exists(fixture.ResolveStoragePath(firstPhoto.StorageKey)));
        var profileFolder = Path.GetDirectoryName(
            fixture.ResolveStoragePath(firstPhoto.StorageKey))!;
        Assert.Equal(
            [fixture.ResolveStoragePath(firstPhoto.StorageKey)],
            Directory.GetFiles(profileFolder));
        Assert.Equal(
            1,
            fixture.Database.AuditLogs.Count(log =>
                log.ActionType == AuditActionType.ProfilePhotoUploaded));
    }

    [Fact]
    public async Task UploadIdentityDocumentAsync_CanceledMetadataSaveRemovesCanonicalFile()
    {
        var interceptor = new ControlledSaveChangesInterceptor();
        await using var fixture = await EmployeeFileFixture.CreateAsync(interceptor);
        using var cancellation = new CancellationTokenSource();
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nemployee document");
        interceptor.FailNext(
            () =>
            {
                cancellation.Cancel();
                return new OperationCanceledException(cancellation.Token);
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Service.UploadIdentityDocumentAsync(
                fixture.EmployeePrincipal,
                1,
                1,
                new EmployeeFileUpload(
                    new MemoryStream(bytes, writable: false),
                    "private.pdf",
                    bytes.LongLength),
                cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Empty(fixture.Database.EmployeeDocuments);
        Assert.DoesNotContain(
            fixture.Database.AuditLogs,
            log => log.ActionType == AuditActionType.EmployeeDocumentUploaded);
        var documentFolder = Path.Combine(
            fixture.RootPath,
            "storage",
            "employees",
            "0000000001",
            "documents",
            EmployeeDocumentCategories.Identity);
        Assert.False(
            Directory.Exists(documentFolder)
            && Directory.EnumerateFiles(
                    documentFolder,
                    "*",
                    SearchOption.AllDirectories)
                .Any());
    }

    [Fact]
    public async Task UploadIdentityDocumentAsync_RejectsAnotherEmployeesRelatedRecord()
    {
        await using var fixture = await EmployeeFileFixture.CreateAsync();
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nemployee document");

        var exception = await Assert.ThrowsAsync<EmployeeFileValidationException>(
            () => fixture.Service.UploadIdentityDocumentAsync(
                fixture.EmployeePrincipal,
                1,
                2,
                new EmployeeFileUpload(
                    new MemoryStream(bytes, writable: false),
                    "payroll.pdf",
                    bytes.LongLength)));

        Assert.Contains("kaydı bulunamadı", exception.Message);
        Assert.Empty(fixture.Database.EmployeeDocuments);
    }

    [Fact]
    public async Task OpenDocumentAsync_DoesNotExposeAnotherEmployeesDocument()
    {
        await using var fixture = await EmployeeFileFixture.CreateAsync();
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nemployee document");
        var document = await fixture.Service.UploadIdentityDocumentAsync(
            fixture.EmployeePrincipal,
            1,
            1,
            new EmployeeFileUpload(
                new MemoryStream(bytes, writable: false),
                "private.pdf",
                bytes.LongLength));

        var otherEmployeeDownload = await fixture.Service.OpenDocumentAsync(
            fixture.OtherEmployeePrincipal,
            document.EmployeeDocumentId);

        Assert.Null(otherEmployeeDownload);
    }

    [Fact]
    public async Task DepartmentManager_CanPreviewEducationButCannotUploadOrDownload()
    {
        await using var fixture = await EmployeeFileFixture.CreateAsync();
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nmanaged employee document");

        var document = await fixture.Service.UploadCourseCertificateDocumentAsync(
            fixture.OtherEmployeePrincipal,
            2,
            2,
            new EmployeeFileUpload(
                new MemoryStream(bytes, writable: false),
                "certificate.pdf",
                bytes.LongLength));

        Assert.Equal(2, document.EmployeeId);
        Assert.Equal(2, document.EmployeeCourseCertificateId);
        var preview = await fixture.Service.OpenDocumentAsync(
            fixture.ManagerPrincipal,
            document.EmployeeDocumentId,
            download: false);
        Assert.NotNull(preview);
        await preview.Content.DisposeAsync();
        Assert.Null(await fixture.Service.OpenDocumentAsync(
            fixture.ManagerPrincipal,
            document.EmployeeDocumentId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Service.UploadCourseCertificateDocumentAsync(
                fixture.ManagerPrincipal,
                2,
                2,
                new EmployeeFileUpload(
                    new MemoryStream(bytes, writable: false),
                    "forbidden.pdf",
                    bytes.LongLength)));
        var accessAudit = Assert.Single(
            fixture.Database.AuditLogs,
            log => log.ActionType == AuditActionType.EmployeeDocumentViewed);
        Assert.Equal(document.EmployeeDocumentId.ToString(), accessAudit.EntityId);
        Assert.Equal(99, accessAudit.ActorEmployeeId);
        Assert.Equal("EmployeeId=2", accessAudit.Details);
    }

    [Fact]
    public async Task OpenDocumentAsync_PreviewIsInlineAndAuditedSeparately()
    {
        await using var fixture = await EmployeeFileFixture.CreateAsync();
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\npreview document");
        var document = await fixture.Service.UploadIdentityDocumentAsync(
            fixture.EmployeePrincipal,
            1,
            1,
            new EmployeeFileUpload(
                new MemoryStream(bytes, writable: false),
                "preview.pdf",
                bytes.LongLength));

        var preview = await fixture.Service.OpenDocumentAsync(
            fixture.EmployeePrincipal,
            document.EmployeeDocumentId,
            download: false);

        Assert.NotNull(preview);
        Assert.Null(preview.DownloadFileName);
        Assert.Equal("application/pdf", preview.ContentType);
        await preview.Content.DisposeAsync();
        var accessAudit = Assert.Single(
            fixture.Database.AuditLogs,
            log => log.ActionType == AuditActionType.EmployeeDocumentViewed);
        Assert.Equal(document.EmployeeDocumentId.ToString(), accessAudit.EntityId);
        Assert.Equal(1, accessAudit.ActorEmployeeId);
    }

    [Fact]
    public async Task OpenDocumentAsync_AuditFailureClosesDocumentAccess()
    {
        var interceptor = new ControlledSaveChangesInterceptor();
        await using var fixture = await EmployeeFileFixture.CreateAsync(interceptor);
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nfail closed document");
        var document = await fixture.Service.UploadIdentityDocumentAsync(
            fixture.EmployeePrincipal,
            1,
            1,
            new EmployeeFileUpload(
                new MemoryStream(bytes, writable: false),
                "fail-closed.pdf",
                bytes.LongLength));

        interceptor.FailNext(
            () => new DbUpdateException("Simulated access audit failure."));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => fixture.Service.OpenDocumentAsync(
                fixture.EmployeePrincipal,
                document.EmployeeDocumentId,
                download: false));
        Assert.DoesNotContain(
            fixture.Database.AuditLogs,
            log => log.ActionType == AuditActionType.EmployeeDocumentViewed);
    }

    [Fact]
    public async Task InactiveCategory_RemainsVisibleForExistingDocumentsButRejectsNewUploads()
    {
        await using var fixture = await EmployeeFileFixture.CreateAsync();
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nemployee document");
        var existingDocument = await fixture.Service.UploadEducationDocumentAsync(
            fixture.EmployeePrincipal,
            1,
            1,
            new EmployeeFileUpload(
                new MemoryStream(bytes, writable: false),
                "certificate.pdf",
                bytes.LongLength));

        var category = await fixture.Database.EmployeeDocumentCategories
            .SingleAsync(item =>
                item.CanonicalKey == EmployeeDocumentCategories.Education);
        category.IsActive = false;
        await fixture.Database.SaveChangesAsync();

        var documents = await fixture.Service.GetDocumentsAsync(
            fixture.EmployeePrincipal,
            1);
        var visibleDocument = Assert.Single(
            documents,
            item => item.EmployeeDocumentId == existingDocument.EmployeeDocumentId);
        Assert.False(visibleDocument.Category.IsActive);

        await Assert.ThrowsAsync<EmployeeFileValidationException>(
            () => fixture.Service.UploadEducationDocumentAsync(
                fixture.EmployeePrincipal,
                1,
                1,
                new EmployeeFileUpload(
                    new MemoryStream(bytes, writable: false),
                    "second-certificate.pdf",
                    bytes.LongLength)));
    }

    [Fact]
    public async Task ContentPolicy_RejectsExtensionSignatureMismatch()
    {
        var bytes = Encoding.ASCII.GetBytes("not a PDF");

        var exception = await Assert.ThrowsAsync<EmployeeFileValidationException>(
            () => EmployeeFileContentPolicy.ValidateDocumentAsync(
                new EmployeeFileUpload(
                    new MemoryStream(bytes, writable: false),
                    "document.pdf",
                    bytes.LongLength)));

        Assert.Contains("eşleşmiyor", exception.Message);
    }

    [Fact]
    public async Task ContentPolicy_RejectsDeclaredOversizeBeforeReading()
    {
        var exception = await Assert.ThrowsAsync<EmployeeFileValidationException>(
            () => EmployeeFileContentPolicy.ValidateProfilePhotoAsync(
                new EmployeeFileUpload(
                    Stream.Null,
                    "photo.png",
                    EmployeeFileContentPolicy.MaxProfilePhotoBytes + 1)));

        Assert.Contains("5 MB", exception.Message);
    }

    [Fact]
    public async Task LocalStore_RejectsKeyOutsideConfiguredFolder()
    {
        await using var fixture = await EmployeeFileFixture.CreateAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Store.SaveAsync(
                "../outside.pdf",
                new MemoryStream("%PDF-"u8.ToArray(), writable: false)));
    }

    [Fact]
    public async Task LocalStore_RemovesTemporaryFileWhenWriteFails()
    {
        await using var fixture = await EmployeeFileFixture.CreateAsync();
        const string canonicalKey =
            "employees/0000000001/documents/other/11111111111111111111111111111111.pdf";

        await Assert.ThrowsAsync<IOException>(
            () => fixture.Store.SaveAsync(
                canonicalKey,
                new FailAfterFirstReadStream("%PDF-partial"u8.ToArray())));

        Assert.False(File.Exists(fixture.ResolveStoragePath(canonicalKey)));
        var directory = Path.GetDirectoryName(fixture.ResolveStoragePath(canonicalKey))!;
        Assert.Empty(Directory.GetFiles(directory, "*.uploading"));
    }

    [Fact]
    public async Task LocalStore_RejectsStorageRootUnderWebRoot()
    {
        await using var fixture = await EmployeeFileFixture.CreateAsync();
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = fixture.RootPath,
            WebRootPath = Path.Combine(fixture.RootPath, "wwwroot")
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => new LocalEmployeeFileStore(
                Options.Create(new EmployeeFileStorageOptions
                {
                    RootPath = "wwwroot/employee-files"
                }),
                environment,
                NullLogger<LocalEmployeeFileStore>.Instance));

        Assert.Contains("wwwroot", exception.Message);
    }

    private sealed class EmployeeFileFixture : IAsyncDisposable
    {
        private EmployeeFileFixture(
            string rootPath,
            HumanResourcesDbContext database,
            LocalEmployeeFileStore store,
            IDbContextFactory<HumanResourcesDbContext> dbContextFactory)
        {
            RootPath = rootPath;
            Database = database;
            Store = store;
            var pageAccessService = new PageAccessService(dbContextFactory);
            Service = new EmployeeFileService(
                dbContextFactory,
                store,
                new PersonnelAuthorizationService(dbContextFactory, pageAccessService),
                NullLogger<EmployeeFileService>.Instance);
        }

        public string RootPath { get; }
        public HumanResourcesDbContext Database { get; }
        public LocalEmployeeFileStore Store { get; }
        public EmployeeFileService Service { get; }
        public ClaimsPrincipal EmployeePrincipal { get; } = CreatePrincipal(1, "employee-1");
        public ClaimsPrincipal OtherEmployeePrincipal { get; } = CreatePrincipal(2, "employee-2");
        public ClaimsPrincipal ManagerPrincipal { get; } = CreateManagerPrincipal();

        public static async Task<EmployeeFileFixture> CreateAsync(
            SaveChangesInterceptor? serviceInterceptor = null)
        {
            var rootPath = Path.Combine(
                Path.GetTempPath(),
                "ik-employee-file-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);

            var databaseName = Guid.NewGuid().ToString("N");
            var databaseRoot = new InMemoryDatabaseRoot();
            var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
                .UseInMemoryDatabase(databaseName, databaseRoot)
                .Options;
            var database = new HumanResourcesDbContext(options);
            await database.Database.EnsureCreatedAsync();

            var department = new Department
            {
                DepartmentId = 1,
                DepartmentName = "Test"
            };
            database.Departments.Add(department);
            database.Employees.AddRange(
                CreateEmployee(1, "001", "1234567890", department),
                CreateEmployee(2, "002", "1234567891", department),
                CreateEmployee(99, "099", "1234567999", department));
            department.ManagerEmployeeId = 99;
            database.EmployeeIdentityDocuments.AddRange(
                new EmployeeIdentityDocument { EmployeeIdentityDocumentId = 1, EmployeeId = 1, DocumentType = "Kimlik", DocumentNumber = "ID-1" },
                new EmployeeIdentityDocument { EmployeeIdentityDocumentId = 2, EmployeeId = 2, DocumentType = "Kimlik", DocumentNumber = "ID-2" });
            database.EmployeeEducations.AddRange(
                new EmployeeEducation { EmployeeEducationId = 1, EmployeeId = 1, InstitutionName = "Test University" },
                new EmployeeEducation { EmployeeEducationId = 2, EmployeeId = 2, InstitutionName = "Other University" });
            database.EmployeeCourseCertificates.AddRange(
                new EmployeeCourseCertificate { EmployeeCourseCertificateId = 1, EmployeeId = 1, Name = "Test Certificate" },
                new EmployeeCourseCertificate { EmployeeCourseCertificateId = 2, EmployeeId = 2, Name = "Other Certificate" });
            await database.SaveChangesAsync();

            var environment = new TestWebHostEnvironment
            {
                ContentRootPath = rootPath,
                WebRootPath = Path.Combine(rootPath, "wwwroot")
            };
            var store = new LocalEmployeeFileStore(
                Options.Create(new EmployeeFileStorageOptions
                {
                    RootPath = "storage"
                }),
                environment,
                NullLogger<LocalEmployeeFileStore>.Instance);

            var serviceOptions = new DbContextOptionsBuilder<HumanResourcesDbContext>()
                .UseInMemoryDatabase(databaseName, databaseRoot);
            if (serviceInterceptor is not null)
            {
                serviceOptions.AddInterceptors(serviceInterceptor);
            }

            return new EmployeeFileFixture(
                rootPath,
                database,
                store,
                new TestDbContextFactory(serviceOptions.Options));
        }

        public string ResolveStoragePath(string canonicalKey)
        {
            return Path.Combine(
                [RootPath, "storage", .. canonicalKey.Split('/')]);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();

            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }

        private static Employee CreateEmployee(
            int employeeId,
            string registryNumber,
            string identityNumber,
            Department department)
        {
            return new Employee
            {
                EmployeeId = employeeId,
                SicilNo = registryNumber,
                FirstName = "Test",
                LastName = $"Employee {employeeId}",
                Email = $"employee{employeeId}@example.com",
                KktcKimlikNo = identityNumber,
                DepartmentId = department.DepartmentId,
                Department = department
            };
        }

        private static ClaimsPrincipal CreatePrincipal(
            int employeeId,
            string userName)
        {
            return new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Name, userName),
                        new Claim(ClaimTypes.NameIdentifier, userName),
                        new Claim(UserClaimTypes.EmployeeId, employeeId.ToString()),
                        new Claim(UserClaimTypes.MustChangePassword, bool.FalseString)
                    ],
                    "Test"));
        }

        private static ClaimsPrincipal CreateManagerPrincipal()
        {
            return new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Name, "manager"),
                        new Claim(ClaimTypes.NameIdentifier, "manager"),
                        new Claim(UserClaimTypes.EmployeeId, "99"),
                        new Claim(UserClaimTypes.MustChangePassword, bool.FalseString)
                    ],
                    "Test"));
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "IK.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<HumanResourcesDbContext> options)
        : IDbContextFactory<HumanResourcesDbContext>
    {
        public HumanResourcesDbContext CreateDbContext() => new(options);

        public Task<HumanResourcesDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class ControlledSaveChangesInterceptor : SaveChangesInterceptor
    {
        private Func<Exception>? nextFailure;

        public void FailNext(Func<Exception> failureFactory)
        {
            ArgumentNullException.ThrowIfNull(failureFactory);
            if (Interlocked.CompareExchange(
                    ref nextFailure,
                    failureFactory,
                    comparand: null) is not null)
            {
                throw new InvalidOperationException(
                    "A simulated SaveChanges failure is already queued.");
            }
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var failureFactory = Interlocked.Exchange(ref nextFailure, null);
            return failureFactory is null
                ? base.SavingChangesAsync(
                    eventData,
                    result,
                    cancellationToken)
                : ValueTask.FromException<InterceptionResult<int>>(
                    failureFactory());
        }
    }

    private sealed class FailAfterFirstReadStream(byte[] firstChunk) : Stream
    {
        private bool hasReturnedFirstChunk;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => firstChunk.LongLength;
        public override long Position
        {
            get => hasReturnedFirstChunk ? firstChunk.LongLength : 0;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (hasReturnedFirstChunk)
            {
                throw new IOException("Simulated read failure.");
            }

            hasReturnedFirstChunk = true;
            var bytesToCopy = Math.Min(count, firstChunk.Length);
            Array.Copy(firstChunk, 0, buffer, offset, bytesToCopy);
            return bytesToCopy;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (hasReturnedFirstChunk)
            {
                throw new IOException("Simulated read failure.");
            }

            hasReturnedFirstChunk = true;
            var bytesToCopy = Math.Min(buffer.Length, firstChunk.Length);
            firstChunk.AsSpan(0, bytesToCopy).CopyTo(buffer.Span);
            return ValueTask.FromResult(bytesToCopy);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
