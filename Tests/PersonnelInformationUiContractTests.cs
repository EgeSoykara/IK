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
        ("EmployeeIdentityDocuments.razor", "/EmployeeIdentityDocuments", "Kimlik Kayıtları"),
        ("EmployeeDocuments.razor", "/EmployeeDocuments", "Tüm Belgeler"),
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
    public void PersonnelInformationPages_KeepTerminationInManagementOnly()
    {
        var nav = ReadRepoFile("Components", "Layout", "NavMenu.razor");
        var tabs = ReadRepoFile("Components", "PersonnelInformationTabs.razor");
        var access = ReadRepoFile("Services", "PageAccessService.cs");

        Assert.Contains("Href=\"/EmployeePersonnelInformation\"", nav);
        Assert.Contains("personnel-nav-entry-active", nav);
        Assert.Contains("private static readonly HashSet<string> PersonnelPaths", nav);
        Assert.Contains("NavigationManager.LocationChanged += HandleLocationChanged", nav);
        Assert.Contains("NavigationManager.LocationChanged -= HandleLocationChanged", nav);
        Assert.DoesNotContain("<MudNavGroup Title=\"Personel Bilgileri\"", nav);
        Assert.Equal(1, nav.Split("Href=\"/EmployeePersonnelInformation\"").Length - 1);
        Assert.DoesNotContain("Href=\"/EmployeeBankAccounts\"", nav);
        Assert.DoesNotContain("Href=\"/EmployeeIdentityDocuments\"", nav);
        Assert.DoesNotContain("Href=\"/EmployeeCourseCertificates\"", nav);
        Assert.Contains("principal.GetEmployeeId().HasValue", access);
        Assert.DoesNotContain("CanEditPersonnelInformation", access);
        Assert.Equal(CommonPages.Length, tabs.Split("new(\"/Employee").Length - 1);

        foreach (var (fileName, route, _) in CommonPages)
        {
            var source = ReadRepoFile("Components", "Pages", fileName);
            Assert.Contains($"@page \"{route}\"", source);
            Assert.Contains("PageAccessService.CanAccessPersonnelInformation(CurrentUser)", source);
            Assert.Contains("<PersonnelInformationTabs", source);
            Assert.Contains($"new(\"{route}\"", tabs);
        }

        var terminationSource = ReadRepoFile("Components", "Pages", "EmployeeTerminations.razor");
        Assert.Contains("@if (_canManageEmployeeTerminations)", nav);
        Assert.Contains("İşten Ayrılmalar", nav);
        Assert.DoesNotContain("ShowTermination", tabs);
        Assert.DoesNotContain("/EmployeeTerminations", tabs);
        Assert.Contains("CanManageEmployeeTerminations(CurrentUser)", terminationSource);
        Assert.Contains("<MudText Typo=\"Typo.overline\">Yönetim</MudText>", terminationSource);
        Assert.DoesNotContain("<PersonnelInformationTabs", terminationSource);
        Assert.Contains("ServerData=\"LoadTerminationsAsync\"", terminationSource);
        Assert.Contains("Label=\"Ayrılma Ekle\"", terminationSource);
        Assert.Contains("SearchEligibleEmployeesAsync", terminationSource);
        Assert.Contains("EmploymentStatusClass(context.Employee.Status)", terminationSource);
        Assert.Contains("FormatEmploymentStatus(context.Employee.Status)", terminationSource);
        Assert.Contains("if (!EnsureCanManageTerminations()) return [];", terminationSource);
        Assert.Contains("return new TableData<EmployeeTermination> { Items = [], TotalItems = 0 };", terminationSource);
        Assert.DoesNotContain("CanAccessPersonnelInformation(CurrentUser)", terminationSource);
    }

    [Fact]
    public void PersonnelInformationTabs_UseResponsiveNonScrollingGrid()
    {
        var tabs = ReadRepoFile("Components", "PersonnelInformationTabs.razor");
        var css = ReadRepoFile("wwwroot", "app.css");

        Assert.Contains("<nav class=\"personnel-tabs\"", tabs);
        var tabsBlock = CssBlock(css, ".personnel-tabs {");
        var tabBlock = CssBlock(css, ".personnel-tabs .personnel-tab {");

        Assert.Contains("display: grid;", tabsBlock);
        Assert.Contains("grid-template-columns: repeat(auto-fit, minmax(155px, 1fr));", tabsBlock);
        Assert.DoesNotContain("overflow-x:", tabsBlock);
        Assert.Contains("justify-content: flex-start;", tabBlock);
        Assert.Contains("min-height: 44px;", tabBlock);
        Assert.Contains("white-space: normal;", tabBlock);
    }

    [Fact]
    public void PersonnelEmployeeSelection_UsesBoundedEmployeeAndDepartmentSearch()
    {
        var selector = ReadRepoFile("Components", "PersonnelEmployeeSelector.razor");
        var lookup = ReadRepoFile("Services", "PersonnelEmployeeLookupService.cs");

        Assert.Contains("<MudAutocomplete T=\"PersonnelEmployeeOption\"", selector);
        Assert.Contains("<MudAutocomplete T=\"PersonnelDepartmentOption\"", selector);
        Assert.Contains("SearchFunc=\"SearchEmployeesAsync\"", selector);
        Assert.Contains("SearchFunc=\"SearchDepartmentsAsync\"", selector);
        Assert.Contains("MinCharacters=\"@PersonnelEmployeeLookupService.MinimumSearchLength\"", selector);
        Assert.Contains("MaxItems=\"@PersonnelEmployeeLookupService.MaximumResults\"", selector);
        Assert.Contains("DebounceInterval=\"300\"", selector);
        Assert.Contains("Label=\"Çalışan ara ve seç\"", selector);
        Assert.Contains("Label=\"Departmana göre listele\"", selector);
        Assert.Contains("personnel-department-browser", selector);
        Assert.Contains("GetDepartmentEmployeesAsync", selector);
        Assert.Contains("@implements IDisposable", selector);
        Assert.Contains("DepartmentLoadCancellation", selector);
        Assert.Contains("cancellation == DepartmentLoadCancellation", selector);
        Assert.Contains("Variant=\"Variant.Outlined\"", selector);
        Assert.Contains("OpenIcon=\"@Icons.Material.Filled.ArrowDropDown\"", selector);
        Assert.Contains("CloseIcon=\"@Icons.Material.Filled.ArrowDropUp\"", selector);
        Assert.Contains("string.IsNullOrWhiteSpace(search)", lookup);
        Assert.Contains("search.Length < MinimumSearchLength", lookup);
        Assert.Contains(".Take(MaximumResults)", lookup);
        Assert.Contains("employee.Department.DepartmentName.Contains(search)", lookup);
        Assert.Contains(".Take(DepartmentPageSize)", lookup);
        Assert.Contains("IDbContextFactory<HumanResourcesDbContext>", lookup);
        Assert.Contains("personnelAuthorizationService.ApplyVisibleEmployees", lookup);

        foreach (var (fileName, _, _) in CommonPages)
        {
            var source = ReadRepoFile("Components", "Pages", fileName);
            Assert.Contains("<PersonnelEmployeeSelector", source);
            Assert.Contains("PersonnelEmployeeLookupService.ResolveInitialAsync", source);
            Assert.DoesNotContain("private List<Employee> Employees", source);
            Assert.DoesNotContain(
                "Where(employee => CanSelectEmployees || employee.EmployeeId",
                source);
        }
    }

    [Fact]
    public void PersonnelDocumentArchive_CentralizesPreviewDownloadAndMaskedRecordContext()
    {
        var archive = ReadRepoFile("Components", "Pages", "EmployeeDocuments.razor");
        var relatedFiles = ReadRepoFile("Components", "RelatedDocumentFiles.razor");
        var program = ReadRepoFile("Program.cs");
        var service = ReadRepoFile("Services", "EmployeeFileService.cs");

        Assert.Contains("<h1>Tüm Belgeler</h1>", archive);
        Assert.Contains("EmployeeFileService.GetDocumentsAsync", archive);
        Assert.Contains("PersonnelSensitiveValue.Mask", archive);
        Assert.Contains("EmployeeFileContentPolicy.CanPreviewDocument", archive);
        Assert.Contains("/employee-files/documents/{context.EmployeeDocumentId}/preview", archive);
        Assert.Contains("/employee-files/documents/{context.EmployeeDocumentId}", archive);
        Assert.Contains("/preview", relatedFiles);
        Assert.Contains("/documents/{documentId:long}/preview", program);
        Assert.Contains("download: false", program);
        Assert.Contains("AuditActionType.EmployeeDocumentViewed", service);
        Assert.Contains("AuditActionType.EmployeeDocumentDownloaded", service);

        var identity = ReadRepoFile("Components", "Pages", "EmployeeIdentityDocuments.razor");
        var banking = ReadRepoFile("Components", "Pages", "EmployeeBankAccounts.razor");
        var personnel = ReadRepoFile("Components", "Pages", "EmployeePersonnelInformation.razor");
        Assert.Contains("@Sensitive(context.DocumentNumber)", identity);
        Assert.Contains("@Sensitive(context.Iban, visiblePrefix: 2)", banking);
        Assert.Contains("@Sensitive(context.AccountNumber)", banking);
        Assert.Contains("@Sensitive(SelectedEmployee.KktcKimlikNo)", personnel);
    }

    [Fact]
    public void PersonnelCrudPages_UseExistingManagementDialogAndActionPattern()
    {
        foreach (var fileName in CrudPages.Append("EmployeeTerminations.razor"))
        {
            var source = ReadRepoFile("Components", "Pages", fileName);
            Assert.Contains("<MudDialog", source);
            Assert.Contains("Class=\"management-data-table\"", source);
            Assert.Contains("<ManagementCreateButton", source);
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
            Assert.Contains("PersonnelAuthorizationService.ResolveAsync", source);
            Assert.Contains("Access.CanEdit", source);
            Assert.Contains("EmployeeId == SelectedEmployeeId.Value", source);
            Assert.Contains("AuditLogService.AppendAsync", source);
            Assert.DoesNotContain("Form.Iban}", source);
            Assert.DoesNotContain("Form.DocumentNumber}", source);
            Assert.DoesNotContain("Form.PhoneNumber}", source);
            Assert.DoesNotContain("Form.AddressLine}", source);
        }
    }

    [Fact]
    public void PersonnelInformationShell_RequiresEmployeeIdentityAndRestrictsTerminationToManagers()
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
        Assert.False(access.CanManageEmployeeTerminations(employee));
        Assert.True(access.CanManageEmployeeTerminations(manager));
    }

    [Fact]
    public void PersonnelInformation_AllowsScopedIdentityGenderAndBloodGroupEditing()
    {
        var source = ReadRepoFile("Components", "Pages", "EmployeePersonnelInformation.razor");

        Assert.Contains("Kişisel Bilgileri Düzenle", source);
        Assert.Contains("Color=\"Color.Info\"", source);
        Assert.Contains("@bind-Value=\"Form.Gender\"", source);
        Assert.Contains("@bind-Value=\"Form.BloodGroup\"", source);
        Assert.Contains("@bind-Value=\"Form.FirstName\"", source);
        Assert.Contains("@bind-Value=\"Form.LastName\"", source);
        Assert.Contains("@bind-Value=\"Form.KktcKimlikNo\"", source);
        Assert.Contains("PersonnelAuthorizationService.ResolveAsync", source);
        Assert.Contains(".CanEditSensitive", source);
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
        Assert.Contains("changedFields.Add(nameof(Employee.FirstName))", source);
        Assert.Contains("changedFields.Add(nameof(Employee.LastName))", source);
        Assert.Contains("changedFields.Add(nameof(Employee.KktcKimlikNo))", source);
        Assert.Contains("AuditActionType.EmployeeUpdated", source);
        Assert.Contains("$\"Fields={string.Join(',', changedFields)}\"", source);
        Assert.DoesNotContain("\"Fields=Gender,BloodGroup\"", source);
        Assert.Contains("Kişisel bilgiler güncellendi.", source);
    }

    [Fact]
    public void Files_AreRemovedFromDashboardAndUploadedOnlyFromTheirRelatedRecord()
    {
        var dashboard = ReadRepoFile("Components", "Pages", "Home.razor");
        var personnel = ReadRepoFile("Components", "Pages", "EmployeePersonnelInformation.razor");
        var identity = ReadRepoFile("Components", "Pages", "EmployeeIdentityDocuments.razor");
        var education = ReadRepoFile("Components", "Pages", "EmployeeEducations.razor");
        var courses = ReadRepoFile("Components", "Pages", "EmployeeCourseCertificates.razor");
        var relatedFiles = ReadRepoFile("Components", "RelatedDocumentFiles.razor");

        Assert.DoesNotContain("<InputFile", dashboard);
        Assert.DoesNotContain("UploadProfilePhotoAsync", dashboard);
        Assert.Contains("EmployeeFileService.UploadProfilePhotoAsync", personnel);
        Assert.Contains("EmployeeFileContentPolicy.MaxProfilePhotoBytes", personnel);
        Assert.Contains("Organizasyon Bilgileri", personnel);
        Assert.Contains("Include(employee => employee.ProfilePhoto)", personnel);

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
                     "AddressHierarchy"
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
        Assert.DoesNotContain("PersonnelSelectOptions.TerminationReasons", terminations);
        Assert.Contains("SearchFunc=\"SearchReasonsAsync\"", terminations);
        Assert.Contains("CoerceValue=\"true\"", terminations);
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

    private static string CssBlock(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(start >= 0, $"CSS selector not found: {selector}");
        var end = css.IndexOf('}', start);
        Assert.True(end > start, $"CSS block is incomplete: {selector}");
        return css[start..(end + 1)];
    }

    private static ClaimsPrincipal PrincipalWithPermission(string permission) =>
        new(new ClaimsIdentity(
            [
                new Claim(PermissionClaimTypes.Permission, permission),
                new Claim(UserClaimTypes.MustChangePassword, bool.FalseString)
            ],
            authenticationType: "test"));

    private static ClaimsPrincipal PrincipalWithEmployeeId(int employeeId) =>
        new(new ClaimsIdentity(
            [
                new Claim(UserClaimTypes.EmployeeId, employeeId.ToString()),
                new Claim(UserClaimTypes.MustChangePassword, bool.FalseString)
            ],
            authenticationType: "test"));
}
