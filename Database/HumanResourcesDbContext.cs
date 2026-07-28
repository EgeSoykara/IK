using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Database;

public sealed class HumanResourcesDbContext : DbContext
{
    public HumanResourcesDbContext(DbContextOptions<HumanResourcesDbContext> options): base(options)
    {
        
    }
    
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveApproval> LeaveApprovals => Set<LeaveApproval>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EmployeeProfilePhoto> EmployeeProfilePhotos => Set<EmployeeProfilePhoto>();
    public DbSet<EmployeeDocumentCategory> EmployeeDocumentCategories => Set<EmployeeDocumentCategory>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeBankAccount> EmployeeBankAccounts => Set<EmployeeBankAccount>();
    public DbSet<EmployeeIdentityDocument> EmployeeIdentityDocuments => Set<EmployeeIdentityDocument>();
    public DbSet<EmployeePhone> EmployeePhones => Set<EmployeePhone>();
    public DbSet<EmployeeAddress> EmployeeAddresses => Set<EmployeeAddress>();
    public DbSet<EmployeeEducation> EmployeeEducations => Set<EmployeeEducation>();
    public DbSet<EmployeeCourseCertificate> EmployeeCourseCertificates => Set<EmployeeCourseCertificate>();
    public DbSet<EmployeeTermination> EmployeeTerminations => Set<EmployeeTermination>();
    public DbSet<ManagerDelegation> ManagerDelegations => Set<ManagerDelegation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EmployeeProfilePhoto>()
            .HasOne(photo => photo.Employee)
            .WithOne(employee => employee.ProfilePhoto)
            .HasForeignKey<EmployeeProfilePhoto>(photo => photo.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmployeeDocument>()
            .HasOne(document => document.Employee)
            .WithMany(employee => employee.Documents)
            .HasForeignKey(document => document.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmployeeDocument>()
            .HasOne(document => document.Category)
            .WithMany(category => category.Documents)
            .HasForeignKey(document => document.CategoryCanonicalKey)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmployeeIdentityDocument>()
            .HasAlternateKey(record => new
            {
                record.EmployeeId,
                record.EmployeeIdentityDocumentId
            })
            .HasName("AK_EmployeeIdentityDocuments_EmployeeId_RecordId");

        modelBuilder.Entity<EmployeeEducation>()
            .HasAlternateKey(record => new
            {
                record.EmployeeId,
                record.EmployeeEducationId
            })
            .HasName("AK_EmployeeEducations_EmployeeId_RecordId");

        modelBuilder.Entity<EmployeeCourseCertificate>()
            .HasAlternateKey(record => new
            {
                record.EmployeeId,
                record.EmployeeCourseCertificateId
            })
            .HasName("AK_EmployeeCourseCertificates_EmployeeId_RecordId");

        modelBuilder.Entity<EmployeeDocument>()
            .HasOne(document => document.IdentityDocument)
            .WithMany(record => record.Documents)
            .HasForeignKey(document => new
            {
                document.EmployeeId,
                document.EmployeeIdentityDocumentId
            })
            .HasPrincipalKey(record => new
            {
                record.EmployeeId,
                record.EmployeeIdentityDocumentId
            })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmployeeDocument>()
            .HasOne(document => document.Education)
            .WithMany(record => record.Documents)
            .HasForeignKey(document => new
            {
                document.EmployeeId,
                document.EmployeeEducationId
            })
            .HasPrincipalKey(record => new
            {
                record.EmployeeId,
                record.EmployeeEducationId
            })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmployeeDocument>()
            .HasOne(document => document.CourseCertificate)
            .WithMany(record => record.Documents)
            .HasForeignKey(document => new
            {
                document.EmployeeId,
                document.EmployeeCourseCertificateId
            })
            .HasPrincipalKey(record => new
            {
                record.EmployeeId,
                record.EmployeeCourseCertificateId
            })
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered indexes cannot be expressed with data annotations. These are the
        // database authority for the single-primary bank, phone and address invariants.
        modelBuilder.Entity<EmployeeBankAccount>()
            .HasIndex(account => new { account.EmployeeId, account.IsPrimary })
            .IsUnique()
            .HasDatabaseName("UX_EmployeeBankAccounts_EmployeeId_Primary")
            .HasFilter("[IsPrimary] = 1");

        modelBuilder.Entity<EmployeePhone>()
            .HasIndex(phone => new { phone.EmployeeId, phone.IsPrimary })
            .IsUnique()
            .HasDatabaseName("UX_EmployeePhones_EmployeeId_Primary")
            .HasFilter("[IsPrimary] = 1");

        modelBuilder.Entity<EmployeeAddress>()
            .HasIndex(address => new { address.EmployeeId, address.IsPrimary })
            .IsUnique()
            .HasDatabaseName("UX_EmployeeAddresses_EmployeeId_Primary")
            .HasFilter("[IsPrimary] = 1");

        modelBuilder.Entity<ManagerDelegation>()
            .HasIndex(delegation => new { delegation.DepartmentId, delegation.IsActive })
            .IsUnique()
            .HasDatabaseName("UX_ManagerDelegations_Department_Active")
            .HasFilter("[IsActive] = 1");

        modelBuilder.Entity<ManagerDelegation>()
            .ToTable(
                "ManagerDelegations",
                table => table.HasCheckConstraint(
                    "CK_ManagerDelegations_DateRange",
                    "[EndDate] >= [StartDate]"));

        modelBuilder.Entity<AuditLog>()
            .ToTable(
                "AuditLogs",
                table => table.HasCheckConstraint(
                    "CK_AuditLogs_ActionType",
                    "[ActionType] BETWEEN 1 AND 31"));

        modelBuilder.Entity<Employee>()
            .ToTable(
                "Employees",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_Employees_Gender",
                        "[Gender] IS NULL OR [Gender] IN (1, 2)");
                    table.HasCheckConstraint(
                        "CK_Employees_BloodGroup",
                        "[BloodGroup] IS NULL OR [BloodGroup] BETWEEN 1 AND 8");
                });

        modelBuilder.Entity<EmployeeDocumentCategory>().HasData(
            new EmployeeDocumentCategory
            {
                CanonicalKey = IK.Web.Models.EmployeeDocumentCategories.Identity,
                DisplayName = "Kimlik Belgeleri",
                SortOrder = 10,
                IsActive = true
            },
            new EmployeeDocumentCategory
            {
                CanonicalKey = IK.Web.Models.EmployeeDocumentCategories.Employment,
                DisplayName = "İş ve Sözleşme Belgeleri",
                SortOrder = 20,
                IsActive = true
            },
            new EmployeeDocumentCategory
            {
                CanonicalKey = IK.Web.Models.EmployeeDocumentCategories.Education,
                DisplayName = "Eğitim ve Sertifika Belgeleri",
                SortOrder = 30,
                IsActive = true
            },
            new EmployeeDocumentCategory
            {
                CanonicalKey = IK.Web.Models.EmployeeDocumentCategories.Health,
                DisplayName = "Sağlık Belgeleri",
                SortOrder = 40,
                IsActive = true
            },
            new EmployeeDocumentCategory
            {
                CanonicalKey = IK.Web.Models.EmployeeDocumentCategories.Other,
                DisplayName = "Diğer Belgeler",
                SortOrder = 50,
                IsActive = true
            });

        modelBuilder.Entity<EmployeeDocumentCategory>()
            .ToTable(
                "EmployeeDocumentCategories",
                table => table.HasCheckConstraint(
                    "CK_EmployeeDocumentCategories_CanonicalKey",
                    "[CanonicalKey] <> ''"
                    + " AND [CanonicalKey] COLLATE Latin1_General_100_BIN2 = LOWER([CanonicalKey])"
                    + " AND [CanonicalKey] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^a-z0-9-]%'"
                    + " AND [CanonicalKey] NOT LIKE '-%'"
                    + " AND [CanonicalKey] NOT LIKE '%-'"
                    + " AND [CanonicalKey] NOT LIKE '%--%'"));

        modelBuilder.Entity<EmployeeProfilePhoto>()
            .ToTable(
                "EmployeeProfilePhotos",
                table => table.HasCheckConstraint(
                    "CK_EmployeeProfilePhotos_SizeBytes",
                    "[SizeBytes] > 0"));

        modelBuilder.Entity<EmployeeDocument>()
            .ToTable(
                "EmployeeDocuments",
                table => table.HasCheckConstraint(
                    "CK_EmployeeDocuments_SizeBytes",
                    "[SizeBytes] > 0"));
    }
}
