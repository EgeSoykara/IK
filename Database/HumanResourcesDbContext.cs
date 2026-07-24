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
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveApproval> LeaveApprovals => Set<LeaveApproval>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EmployeeProfilePhoto> EmployeeProfilePhotos => Set<EmployeeProfilePhoto>();
    public DbSet<EmployeeDocumentCategory> EmployeeDocumentCategories => Set<EmployeeDocumentCategory>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();

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

        modelBuilder.Entity<AuditLog>()
            .ToTable(
                "AuditLogs",
                table => table.HasCheckConstraint(
                    "CK_AuditLogs_ActionType",
                    "[ActionType] BETWEEN 1 AND 24"));

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
