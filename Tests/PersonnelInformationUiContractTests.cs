namespace IK.Web.Tests;

using System.Security.Claims;
using IK.Web.Components;
using IK.Web.Database;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;

public sealed class PersonnelInformationUiContractTests
{
    private static readonly (string FileName, string Route, string NavLabel)[] CommonPages =
    [
        ("EmployeePersonnelInformation.razor", "/EmployeePersonnelInformation", "Personel Bilgileri"),
        ("EmployeeBankAccounts.razor", "/EmployeeBankAccounts", "Banka Bilgileri"),
        ("EmployeeGeneralInformation.razor", "/EmployeeGeneralInformation", "Genel Bilgiler"),
        ("EmployeeIdentityDocuments.razor", "/EmployeeIdentityDocuments", "Kimlik ve Belgeler"),
        ("EmployeePhones.razor", "/EmployeePhones", "Telefonlar"),
        ("EmployeeAddresses.razor", "/EmployeeAddresses", "Adresler"),
        ("EmployeeEducations.razor", "/EmployeeEducations", "Eğitimler"),
        ("EmployeeCourseCertificates.razor", "/EmployeeCourseCertificates", "Kurslar ve Sertifikalar")
    ];

    private static readonly string[] CrudPages =
    [
        "EmployeeBankAccounts.razor",
        "EmployeeIdentityDocuments.razor",
        "EmployeePhones.razor",
        "EmployeeAddresses.razor",
        "EmployeeEducations.razor",
        "EmployeeCourseCertificates.razor"
    ];

    [Fact]
    public void PersonnelInformationPages_AreSharedTabsAndNormalUsersDoNotSeeTermination()
    {
        var nav = ReadRepoFile("Components", "Layout", "NavMenu.razor");
        var tabs = ReadRepoFile("Components", "PersonnelInformationTabs.razor");
        var access = ReadRepoFile("Services", "PageAccessService.cs");

        Assert.Contains("Title=\"Personel Bilgileri\"", nav);
        Assert.Contains("principal.GetEmployeeId().HasValue", access);
        Assert.Contains("CanEditPersonnelInformation", access);

        foreach (var (fileName, route, navLabel) in CommonPages)
        {
            var source = ReadRepoFile("Components", "Pages", fileName);
            Assert.Contains($"@page \"{route}\"", source);
            Assert.Contains("PageAccessService.CanAccessPersonnelInformation(CurrentUser)", source);
            Assert.Contains("<PersonnelInformationTabs", source);
            Assert.Contains($"Href=\"{route}\"", nav);
            Assert.Contains(navLabel, nav);
            Assert.Contains($"new(\"{route}\"", tabs);
        }

        Assert.Contains("@if (_canManageEmployeeTerminations)", nav);
        Assert.Contains("ShowTermination", tabs);
        Assert.Contains("CanManageEmployeeTerminations(CurrentUser)", ReadRepoFile("Components", "Pages", "EmployeeTerminations.razor"));
        Assert.DoesNotContain("CanAccessPersonnelInformation(CurrentUser)", ReadRepoFile("Components", "Pages", "EmployeeTerminations.razor"));
    }

    [Fact]
    public void PersonnelCrudPages_UseExistingManagementDialogAndActionPattern()
    {
        foreach (var fileName in CrudPages.Append("EmployeeTerminations.razor"))
        {
            var source = ReadRepoFile("Components", "Pages", fileName);
            Assert.Contains("<MudDialog", source);
            Assert.Contains("Class=\"management-data-table\"", source);
            Assert.Contains("<MudFab", source);
            Assert.Contains("<MudTooltip", source);
            Assert.Contains("Variant=\"Variant.Outlined\"", source);
            Assert.Contains("DataAnnotationsValidator", source);
            Assert.DoesNotContain("Class=\"pa-4 mb-4\"", source);
        }
    }

    [Fact]
    public void PersonnelInformationMutations_AreEmployeeScopedAndAuditedWithoutSensitiveValues()
    {
        foreach (var fileName in CrudPages)
        {
            var source = ReadRepoFile("Components", "Pages", fileName);
            Assert.True(
                source.Split("PageAccessService.CanEditPersonnelInformation")
                    .Length >= 4,
                $"{fileName} must guard selection, load and mutations.");
            Assert.Contains("EmployeeId == SelectedEmployeeId.Value", source);
            Assert.Contains("AuditLogService.AppendAsync", source);
            Assert.DoesNotContain("Form.Iban}", source);
            Assert.DoesNotContain("Form.DocumentNumber}", source);
            Assert.DoesNotContain("Form.PhoneNumber}", source);
            Assert.DoesNotContain("Form.AddressLine}", source);
        }
    }

    [Fact]
    public void PersonnelInformationPermission_AllowsOwnEmployeeAndRestrictsTerminationToManagers()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseSqlServer("Server=localhost;Database=PermissionContract;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new HumanResourcesDbContext(options);
        var access = new PageAccessService(
            TestHumanResourcesDbContextFactory.From(dbContext));

        var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity());
        var searchOnly = PrincipalWithPermission(PermissionNames.CanviewEmployeeSearch);
        var employee = PrincipalWithEmployeeId(42);
        var manager = PrincipalWithPermission(PermissionNames.CanCreateNewEmployee);

        Assert.False(access.CanAccessPersonnelInformation(unauthenticated));
        Assert.False(access.CanAccessPersonnelInformation(searchOnly));
        Assert.True(access.CanAccessPersonnelInformation(employee));
        Assert.True(access.CanAccessPersonnelInformation(manager));
        Assert.True(access.CanEditPersonnelInformation(employee, 42));
        Assert.False(access.CanEditPersonnelInformation(employee, 43));
        Assert.True(access.CanEditPersonnelInformation(manager, 43));
        Assert.False(access.CanManageEmployeeTerminations(employee));
        Assert.True(access.CanManageEmployeeTerminations(manager));
    }

    [Fact]
    public void PersonnelInformation_AllowsScopedGenderAndBloodGroupEditing()
    {
        var source = ReadRepoFile("Components", "Pages", "EmployeePersonnelInformation.razor");

        Assert.Contains("Kişisel Bilgileri Düzenle", source);
        Assert.Contains("@bind-Value=\"Form.Gender\"", source);
        Assert.Contains("@bind-Value=\"Form.BloodGroup\"", source);
        Assert.Contains("PageAccessService.CanEditPersonnelInformation(CurrentUser, SelectedEmployeeId.Value)", source);
        Assert.Contains("employee.Gender = Form.Gender", source);
        Assert.Contains("employee.BloodGroup = Form.BloodGroup", source);
        Assert.Contains("RowVersion = SelectedEmployee.RowVersion.ToArray()", source);
        Assert.Contains("employee.RowVersion.SequenceEqual(Form.RowVersion)", source);
        Assert.Contains("catch (DbUpdateConcurrencyException)", source);
        Assert.Contains("if (IsSaving)", source);
        Assert.Contains("Disabled=\"@IsSaving\"", source);
        Assert.Contains("if (changedFields.Count == 0)", source);
        Assert.Contains("changedFields.Add(nameof(Employee.Gender))", source);
        Assert.Contains("changedFields.Add(nameof(Employee.BloodGroup))", source);
        Assert.Contains("AuditActionType.EmployeeUpdated", source);
        Assert.Contains("$\"Fields={string.Join(',', changedFields)}\"", source);
        Assert.DoesNotContain("\"Fields=Gender,BloodGroup\"", source);
        Assert.Contains("Cinsiyet ve kan grubu bilgileri güncellendi.", source);
    }

    [Fact]
    public void Files_AreRemovedFromDashboardAndUploadedOnlyFromTheirRelatedRecord()
    {
        var dashboard = ReadRepoFile("Components", "Pages", "Home.razor");
        var general = ReadRepoFile("Components", "Pages", "EmployeeGeneralInformation.razor");
        var identity = ReadRepoFile("Components", "Pages", "EmployeeIdentityDocuments.razor");
        var education = ReadRepoFile("Components", "Pages", "EmployeeEducations.razor");
        var courses = ReadRepoFile("Components", "Pages", "EmployeeCourseCertificates.razor");
        var relatedFiles = ReadRepoFile("Components", "RelatedDocumentFiles.razor");

        Assert.DoesNotContain("<InputFile", dashboard);
        Assert.DoesNotContain("UploadProfilePhotoAsync", dashboard);
        Assert.Contains("EmployeeFileService.UploadProfilePhotoAsync", general);
        Assert.Contains("EmployeeFileContentPolicy.MaxProfilePhotoBytes", general);

        Assert.Contains("UploadIdentityDocumentAsync", identity);
        Assert.Contains("SelectedIdentityDocumentId", identity);
        Assert.Contains("UploadEducationDocumentAsync", education);
        Assert.Contains("SelectedEducationId", education);
        Assert.Contains("UploadCourseCertificateDocumentAsync", courses);
        Assert.Contains("SelectedCourseId", courses);
        Assert.Equal(1, relatedFiles.Split("<InputFile id=").Length - 1);
        Assert.DoesNotContain("Belge kategorisi", identity);
        Assert.DoesNotContain("SelectedDocumentCategoryKey", identity);
    }

    [Fact]
    public void OpenEndedPersonnelFields_UseSimpleTextEntry()
    {
        var fieldsByPage = new Dictionary<string, string[]>
        {
            ["EmployeeBankAccounts.razor"] = ["Form.BankName", "Form.BranchName"],
            ["EmployeeEducations.razor"] = ["Form.InstitutionName", "Form.DepartmentName"],
            ["EmployeeCourseCertificates.razor"] = ["Form.IssuingOrganization"],
            ["EmployeeIdentityDocuments.razor"] = ["Form.IssuingAuthority"]
        };

        foreach (var (fileName, fields) in fieldsByPage)
        {
            var source = ReadRepoFile("Components", "Pages", fileName);
            foreach (var field in fields)
            {
                Assert.Contains($"<MudTextField T=\"string\" @bind-Value=\"{field}\"", source);
            }

            Assert.DoesNotContain("<MudAutocomplete", source);
            Assert.DoesNotContain("ManagementFilterOptionSearch", source);
            Assert.DoesNotContain("LoadLookupOptionsAsync", source);
        }
    }

    [Fact]
    public void CategoricalFields_UseOneManualSelectOptionAuthority()
    {
        var options = ReadRepoFile("Components", "PersonnelSelectOptions.cs");
        var guide = ReadRepoFile("Docs", "personnel-select-options.md");
        var identity = ReadRepoFile("Components", "Pages", "EmployeeIdentityDocuments.razor");
        var education = ReadRepoFile("Components", "Pages", "EmployeeEducations.razor");
        var phones = ReadRepoFile("Components", "Pages", "EmployeePhones.razor");
        var addresses = ReadRepoFile("Components", "Pages", "EmployeeAddresses.razor");
        var terminations = ReadRepoFile("Components", "Pages", "EmployeeTerminations.razor");

        foreach (var optionName in new[]
                 {
                     "DocumentTypes",
                     "EducationLevels",
                     "PhoneTypes",
                     "AddressTypes",
                     "AddressHierarchy",
                     "TerminationReasons"
                 })
        {
            Assert.Contains($"PersonnelSelectOptions.{optionName}", string.Concat(
                identity, education, phones, addresses, terminations));
            Assert.Contains(optionName, options);
            Assert.Contains(optionName, guide);
        }

        Assert.DoesNotContain("SearchDocumentTypesAsync", identity);
        Assert.DoesNotContain("ExistingDocumentTypes", identity);
        Assert.DoesNotContain("SearchDistrictsAsync", addresses);
        Assert.DoesNotContain("SearchCitiesAsync", addresses);
        Assert.DoesNotContain("SearchCountriesAsync", addresses);
        Assert.Contains("Disabled=\"@(!PersonnelSelectOptions.DocumentTypes.Any())\"", identity);
        Assert.Contains("Disabled=\"@(!PersonnelSelectOptions.EducationLevels.Any())\"", education);
        Assert.Contains("Disabled=\"@(!PersonnelSelectOptions.PhoneTypes.Any())\"", phones);
        Assert.Contains("Disabled=\"@(!PersonnelSelectOptions.TerminationReasons.Any())\"", terminations);
    }

    [Fact]
    public void EducationLevels_AreAvailableInNormalApplicationBuild()
    {
        Assert.Equal(
            ["Ön Lisans", "Lisans", "Yüksek Lisans", "Doktora"],
            PersonnelSelectOptions.EducationLevels);
        Assert.True(PersonnelSelectOptions.IsEducationLevelAllowed("Lisans"));
        Assert.False(PersonnelSelectOptions.IsEducationLevelAllowed("Lise"));
        Assert.False(PersonnelSelectOptions.IsEducationLevelAllowed(null));

        var education = ReadRepoFile("Components", "Pages", "EmployeeEducations.razor");
        Assert.Contains(
            "PersonnelSelectOptions.IsEducationLevelAllowed(Form.EducationLevel)",
            education);
    }

    [Fact]
    public void AddressAndEducationForms_ExposeTheConfirmedSemantics()
    {
        var addresses = ReadRepoFile("Components", "Pages", "EmployeeAddresses.razor");
        var education = ReadRepoFile("Components", "Pages", "EmployeeEducations.razor");
        var educationModel = ReadRepoFile("Models", "EmployeeEducation.cs");

        var addressType = addresses.IndexOf("Label=\"1. Adres Türü\"", StringComparison.Ordinal);
        var country = addresses.IndexOf("Label=\"2. Ülke\"", StringComparison.Ordinal);
        var city = addresses.IndexOf("Label=\"3. Şehir\"", StringComparison.Ordinal);
        var district = addresses.IndexOf("Label=\"4. İlçe/Bölge\"", StringComparison.Ordinal);
        var postalCode = addresses.IndexOf("Label=\"5. Posta Kodu\"", StringComparison.Ordinal);
        var addressLine = addresses.IndexOf("Label=\"6. Açık Adres\"", StringComparison.Ordinal);

        Assert.True(addressType < country && country < city && city < district);
        Assert.True(district < postalCode && postalCode < addressLine);
        Assert.Contains("Form.City = null;", addresses);
        Assert.Equal(2, addresses.Split("Form.District = null;").Length - 1);
        Assert.Contains("CitiesFor(Form.Country)", addresses);
        Assert.Contains("DistrictsFor(Form.Country, Form.City)", addresses);

        Assert.Contains("Label=\"Eğitim Seviyesi\"", education);
        Assert.Contains("@bind-Value=\"Form.EducationLevel\"", education);
        Assert.Contains("entity.EducationLevel = Form.EducationLevel", education);
        Assert.Contains("public string? EducationLevel", educationModel);
        Assert.Contains("Label=\"Diploma/Derece Adı\"", education);
    }

    [Fact]
    public void LegacyUnlinkedDocuments_HaveOneSectionAuthority()
    {
        var identity = ReadRepoFile("Components", "Pages", "EmployeeIdentityDocuments.razor");
        var education = ReadRepoFile("Components", "Pages", "EmployeeEducations.razor");

        Assert.Contains("document.CategoryCanonicalKey != EmployeeDocumentCategories.Education", identity);
        Assert.Contains("document.CategoryCanonicalKey == EmployeeDocumentCategories.Education", education);
    }

    [Fact]
    public void EmployeeDeletion_BlocksAllPersonnelInformationRelationships()
    {
        var source = ReadRepoFile("Components", "Pages", "Employees.razor");
        foreach (var dbSet in new[]
                 {
                     "EmployeeBankAccounts",
                     "EmployeeIdentityDocuments",
                     "EmployeePhones",
                     "EmployeeAddresses",
                     "EmployeeEducations",
                     "EmployeeCourseCertificates",
                     "EmployeeTerminations"
                 })
        {
            Assert.Contains($"Database.{dbSet}.AnyAsync", source);
        }
        Assert.Contains("ilişkili personel bilgileri bulunuyor", source);
    }

    [Fact]
    public void PersonnelInformationPages_DoNotExposeExcelActions()
    {
        foreach (var page in new[]
                 {
                     "EmployeePersonnelInformation.razor",
                     "EmployeeBankAccounts.razor",
                     "EmployeeGeneralInformation.razor",
                     "EmployeeIdentityDocuments.razor",
                     "EmployeePhones.razor",
                     "EmployeeAddresses.razor",
                     "EmployeeEducations.razor",
                     "EmployeeCourseCertificates.razor",
                     "EmployeeTerminations.razor"
                 })
        {
            Assert.DoesNotContain(
                "ExcelImportExportActions",
                ReadRepoFile("Components", "Pages", page));
        }

        Assert.Contains(
            "ExcelImportExportActions",
            ReadRepoFile("Components", "Pages", "Employees.razor"));
        Assert.Contains(
            "ExcelImportExportActions",
            ReadRepoFile("Components", "Pages", "PublicHolidays.razor"));
    }

    [Fact]
    public void PersonnelInformationE2eHarness_IsPortableAndRejectsSharedDatabases()
    {
        var script = ReadRepoFile(".bet-task", "scripts", "run-employee-information-e2e.sh");
        var fixture = ReadRepoFile(".bet-task", "e2e", "FixtureTool", "Program.cs");

        Assert.Contains("DOTNET_HOST_PATH:-$(command -v dotnet", script);
        Assert.DoesNotContain("/Users/", script);
        Assert.Contains("IK_E2E_CONNECTION_STRING", script);
        Assert.Contains("\"$fixture_dll\" validate", script);
        Assert.Contains("\"$fixture_dll\" setup", script);
        Assert.Contains("\"$fixture_dll\" teardown", script);
        Assert.Contains("IK_E2E_BASE_URL zaten kullanımda", script);
        Assert.Contains("kill -0 \"$server_pid\"", script);
        Assert.Contains("EnsureDeletedAsync", fixture);
        Assert.Contains("StartsWith(\"IK_E2E_\"", fixture);
    }

    private static string ReadRepoFile(params string[] pathSegments)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([repositoryRoot, .. pathSegments]));
    }

    private static ClaimsPrincipal PrincipalWithPermission(string permission) =>
        new(new ClaimsIdentity(
            [new Claim(PermissionClaimTypes.Permission, permission)],
            authenticationType: "test"));

    private static ClaimsPrincipal PrincipalWithEmployeeId(int employeeId) =>
        new(new ClaimsIdentity(
            [new Claim(UserClaimTypes.EmployeeId, employeeId.ToString())],
            authenticationType: "test"));
}
