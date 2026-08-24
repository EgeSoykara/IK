using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IK.Web.Database;

public sealed class HumanResourcesDbContext : DbContext
{
    public HumanResourcesDbContext(DbContextOptions<HumanResourcesDbContext> options) : base(options)
    {

    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ApplicationRole> ApplicationRoles => Set<ApplicationRole>();
    public DbSet<ApplicationRolePermission> ApplicationRolePermissions => Set<ApplicationRolePermission>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveRequestBalanceAllocation> LeaveRequestBalanceAllocations => Set<LeaveRequestBalanceAllocation>();
    public DbSet<LeaveRequestApprovedDay> LeaveRequestApprovedDays => Set<LeaveRequestApprovedDay>();
    public DbSet<LeaveCancellationRequest> LeaveCancellationRequests => Set<LeaveCancellationRequest>();
    public DbSet<LeaveCancellationApproval> LeaveCancellationApprovals => Set<LeaveCancellationApproval>();
    public DbSet<LeaveCancellationBalanceRefund> LeaveCancellationBalanceRefunds => Set<LeaveCancellationBalanceRefund>();
    public DbSet<LeaveCarryOverWarning> LeaveCarryOverWarnings => Set<LeaveCarryOverWarning>();
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

        modelBuilder.Entity<AuditLog>()
            .ToTable(tableBuilder => tableBuilder.HasCheckConstraint(
                "CK_AuditLogs_Actor",
                "([ActorEmployeeId] IS NOT NULL AND [SystemActorKey] IS NULL) OR "
                + "([ActorEmployeeId] IS NULL AND [SystemActorKey] IS NOT NULL AND [SystemActorKey] <> '')"));

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
            .HasIndex(delegation => delegation.LeaveRequestId)
            .IsUnique()
            .HasDatabaseName("UX_ManagerDelegations_LeaveRequest")
            .HasFilter("[LeaveRequestId] IS NOT NULL AND [RestoredAt] IS NULL");

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
                    "[ActionType] BETWEEN 1 AND 44"));

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

        modelBuilder.Entity<Employee>()
            .HasIndex(employee => employee.Email)
            .IsUnique()
            .HasDatabaseName("UX_Employees_Email");

        modelBuilder.Entity<Employee>()
            .HasIndex(employee => employee.SamAccountName)
            .IsUnique()
            .HasDatabaseName("UX_Employees_SamAccountName")
            .HasFilter("[SamAccountName] IS NOT NULL");

        modelBuilder.Entity<Employee>()
            .HasIndex(employee => employee.SicilNo)
            .IsUnique()
            .HasDatabaseName("UX_Employees_SicilNo")
            .HasFilter("[SicilNo] IS NOT NULL");

        modelBuilder.Entity<Employee>()
            .HasIndex(employee => employee.KktcKimlikNo)
            .IsUnique()
            .HasDatabaseName("UX_Employees_KktcKimlikNo")
            .HasFilter("[KKTC_KimlikNo] IS NOT NULL");

        // Enum ranges cannot be expressed as a database constraint with data
        // annotations; keep the persisted worker discriminator fail-closed.
        modelBuilder.Entity<LeaveType>()
            .ToTable(
                "LeaveTypes",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_LeaveTypes_EntitlementKind",
                        "[EntitlementKind] BETWEEN 0 AND 6");
                    table.HasCheckConstraint(
                        "CK_LeaveTypes_PositiveId",
                        "[LeaveTypeId] > 0");
                    table.HasCheckConstraint(
                        "CK_LeaveTypes_HalfDayAmounts",
                        "[AnnualQuota] >= 0 AND [AnnualQuota] * 2 = FLOOR([AnnualQuota] * 2)"
                        + " AND [MaxAccrualDays] > 0 AND [MaxAccrualDays] * 2 = FLOOR([MaxAccrualDays] * 2)");
                    table.HasCheckConstraint(
                        "CK_LeaveTypes_MobilizationPolicy",
                        "[EntitlementKind] <> 6"
                        + " OR ([AnnualQuota] <= 2 AND [CarryOverRule] = 0 AND [MaxAccrualDays] <= 2)");
                });

        modelBuilder.Entity<LeaveBalance>()
            .ToTable(
                "LeaveBalances",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_LeaveBalances_HalfDayAmounts",
                        "[EntitledDays] >= 0 AND [EntitledDays] * 2 = FLOOR([EntitledDays] * 2)"
                        + " AND [CarryOverDays] >= 0 AND [CarryOverDays] * 2 = FLOOR([CarryOverDays] * 2)"
                        + " AND [UsedDays] >= 0 AND [UsedDays] * 2 = FLOOR([UsedDays] * 2)"
                        + " AND [RemainingDays] >= 0 AND [RemainingDays] * 2 = FLOOR([RemainingDays] * 2)"
                        + " AND [RemainingDays] = [EntitledDays] + [CarryOverDays] - [UsedDays]");
                    table.HasCheckConstraint(
                        "CK_LeaveBalances_MobilizationMaximum",
                        "[LeaveTypeId] <> 6 OR [EntitledDays] + [CarryOverDays] <= 2");
                });

        modelBuilder.Entity<LeaveRequest>()
            .ToTable(
                "LeaveRequests",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_LeaveRequests_Category",
                        "[Category] IN (1, 2)");
                    table.HasCheckConstraint(
                        "CK_LeaveRequests_LeaveTypeSelection",
                        "([Category] = 1 AND [LeaveTypeId] IS NULL)"
                        + " OR ([Category] = 2 AND [LeaveTypeId] IS NOT NULL AND [LeaveTypeId] > 0)");
                    table.HasCheckConstraint(
                        "CK_LeaveRequests_HalfDayAmount",
                        "[RequestedDays] > 0 AND [RequestedDays] * 2 = FLOOR([RequestedDays] * 2)");
                    table.HasCheckConstraint(
                        "CK_LeaveRequests_CurrentStatus",
                        "[CurrentStatus] IN (1, 2, 3, 4, 5)");
                });

        modelBuilder.Entity<LeaveCancellationRequest>()
            .ToTable(
                "LeaveCancellationRequests",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_LeaveCancellationRequests_DateRange",
                        "[CancellationStartDate] <= [CancellationEndDate]");
                    table.HasCheckConstraint(
                        "CK_LeaveCancellationRequests_RefundDays",
                        "[RequestedRefundDays] > 0 AND [RequestedRefundDays] * 2 = FLOOR([RequestedRefundDays] * 2)");
                    table.HasCheckConstraint(
                        "CK_LeaveCancellationRequests_CurrentStatus",
                        "[CurrentStatus] IN (1, 2, 3, 4)");
                });

        modelBuilder.Entity<LeaveRequestApprovedDay>()
            .ToTable(
                "LeaveRequestApprovedDays",
                table => table.HasCheckConstraint(
                    "CK_LeaveRequestApprovedDays_Days",
                    "[Days] IN (0.5, 1.0)"));

        modelBuilder.Entity<LeaveCancellationBalanceRefund>()
            .ToTable(
                "LeaveCancellationBalanceRefunds",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_LeaveCancellationBalanceRefunds_Source",
                        "[Source] IN (1, 2)");
                    table.HasCheckConstraint(
                        "CK_LeaveCancellationBalanceRefunds_Days",
                        "[Days] > 0 AND [Days] * 2 = FLOOR([Days] * 2)");
                });

        modelBuilder.Entity<LeaveCancellationApproval>()
            .ToTable(
                "LeaveCancellationApprovals",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_LeaveCancellationApprovals_ApproverRole",
                        "[ApproverRole] IN (1, 2)");
                    table.HasCheckConstraint(
                        "CK_LeaveCancellationApprovals_Decision",
                        "[Decision] IN (1, 2, 3)");
                    table.HasCheckConstraint(
                        "CK_LeaveCancellationApprovals_RejectionComment",
                        "[Decision] <> 3 OR NULLIF(LTRIM(RTRIM([Comment])), '') IS NOT NULL");
                });

        modelBuilder.Entity<LeaveRequestBalanceAllocation>()
            .ToTable(
                "LeaveRequestBalanceAllocations",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_LeaveRequestBalanceAllocations_Source",
                        "[Source] IN (1, 2)");
                    table.HasCheckConstraint(
                        "CK_LeaveRequestBalanceAllocations_HalfDayAmount",
                        "[Days] > 0 AND [Days] * 2 = FLOOR([Days] * 2)");
                });

        modelBuilder.Entity<LeaveCarryOverWarning>()
            .ToTable(
                "LeaveCarryOverWarnings",
                table => table.HasCheckConstraint(
                    "CK_LeaveCarryOverWarnings_HalfDayAmounts",
                    "[CarryOverDays] >= 0 AND [CarryOverDays] * 2 = FLOOR([CarryOverDays] * 2)"
                    + " AND [EntitledDays] >= 0 AND [EntitledDays] * 2 = FLOOR([EntitledDays] * 2)"
                    + " AND [TotalDays] >= 0 AND [TotalDays] * 2 = FLOOR([TotalDays] * 2)"
                    + " AND [WarningLimitDays] > 0 AND [WarningLimitDays] * 2 = FLOOR([WarningLimitDays] * 2)"
                    + " AND [TotalDays] = [EntitledDays] + [CarryOverDays]"));

        modelBuilder.Entity<ApplicationRole>().HasData(
            new ApplicationRole
            {
                ApplicationRoleId = ApplicationRoleDefaults.EmployeeRoleId,
                Name = ApplicationRoleDefaults.EmployeeName,
                Description = "Standart çalışan erişimi"
            },
            new ApplicationRole
            {
                ApplicationRoleId = ApplicationRoleDefaults.ManagerRoleId,
                Name = ApplicationRoleDefaults.ManagerName,
                Description = "Departman yöneticisi erişimi"
            },
            new ApplicationRole
            {
                ApplicationRoleId = ApplicationRoleDefaults.HumanResourcesRoleId,
                Name = ApplicationRoleDefaults.HumanResourcesName,
                Description = "İnsan kaynakları yönetimi"
            },
            new ApplicationRole
            {
                ApplicationRoleId = ApplicationRoleDefaults.SystemAdministratorRoleId,
                Name = ApplicationRoleDefaults.SystemAdministratorName,
                Description = "Tam uygulama ve personel yönetimi"
            });

        var managerPermissions = new[]
            {
                PermissionNames.CanViewLeaveRequests,
                PermissionNames.CanManageLeaveRequests,
                PermissionNames.CanExectuteApproveLeave
            }
            .Select(permission => new ApplicationRolePermission
            {
                ApplicationRoleId = ApplicationRoleDefaults.ManagerRoleId,
                PermissionName = permission
            });
        var elevatedPermissions = PermissionNames.All
            .SelectMany(permission => new[]
            {
                new ApplicationRolePermission
                {
                    ApplicationRoleId = ApplicationRoleDefaults.SystemAdministratorRoleId,
                    PermissionName = permission
                },
                new ApplicationRolePermission
                {
                    ApplicationRoleId = ApplicationRoleDefaults.HumanResourcesRoleId,
                    PermissionName = permission
                }
            })
            .Where(permission =>
                permission.ApplicationRoleId != ApplicationRoleDefaults.SystemAdministratorRoleId
                || permission.PermissionName != PermissionNames.CanActAsHumanResources);
        var rolePermissions = managerPermissions
            .Concat(elevatedPermissions)
            .Append(new ApplicationRolePermission
            {
                ApplicationRoleId = ApplicationRoleDefaults.EmployeeRoleId,
                PermissionName = PermissionNames.CanManageLeaveRequests
            })
            .ToArray();
        modelBuilder.Entity<ApplicationRolePermission>().HasData(rolePermissions);

        modelBuilder.Entity<LeaveType>().HasData(
            new LeaveType
            {
                LeaveTypeId = 1,
                Name = "0-10 Yıllık Çalışan İzni",
                AnnualQuota = 30m,
                CarryOverRule = true,
                MaxAccrualDays = DomainConstants.MaxLeaveAccrualWarningDays,
                EntitlementKind = LeaveEntitlementKind.ServiceYears0To10
            },
            new LeaveType
            {
                LeaveTypeId = 2,
                Name = "10-20 Yıllık Çalışan İzni",
                AnnualQuota = 30m,
                CarryOverRule = true,
                MaxAccrualDays = DomainConstants.MaxLeaveAccrualWarningDays,
                EntitlementKind = LeaveEntitlementKind.ServiceYears10To20
            },
            new LeaveType
            {
                LeaveTypeId = 3,
                Name = "20-30 Yıllık Çalışan İzni",
                AnnualQuota = 30m,
                CarryOverRule = true,
                MaxAccrualDays = DomainConstants.MaxLeaveAccrualWarningDays,
                EntitlementKind = LeaveEntitlementKind.ServiceYears20Plus
            },
            new LeaveType
            {
                LeaveTypeId = 4,
                Name = "Hastalık İzni",
                AnnualQuota = 30m,
                CarryOverRule = false,
                MaxAccrualDays = DomainConstants.MaxLeaveAccrualWarningDays,
                EntitlementKind = LeaveEntitlementKind.AllEmployees
            },
            new LeaveType
            {
                LeaveTypeId = 5,
                Name = "Hamilelik İzni",
                AnnualQuota = 30m,
                CarryOverRule = false,
                MaxAccrualDays = DomainConstants.MaxLeaveAccrualWarningDays,
                EntitlementKind = LeaveEntitlementKind.FemaleEmployees
            },
            new LeaveType
            {
                LeaveTypeId = 6,
                Name = "Seferberlik İzni",
                AnnualQuota = DomainConstants.MobilizationLeaveMaximumDays,
                CarryOverRule = false,
                MaxAccrualDays = DomainConstants.MobilizationLeaveMaximumDays,
                EntitlementKind = LeaveEntitlementKind.MaleEmployees
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

public sealed class HumanResourcesDbContextFactory
    : IDesignTimeDbContextFactory<HumanResourcesDbContext>
{
    public HumanResourcesDbContext CreateDbContext(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environments.Development;
        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true);
        if (environmentName == Environments.Development)
        {
            configurationBuilder.AddUserSecrets<HumanResourcesDbContextFactory>(optional: true);
        }

        var configuration = configurationBuilder
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("HumanResources")
            ?? throw new InvalidOperationException(
                "HumanResources veritabanı bağlantı dizesi yapılandırılmalıdır.");
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new HumanResourcesDbContext(options);
    }
}
