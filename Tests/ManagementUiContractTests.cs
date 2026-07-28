namespace IK.Web.Tests;

public sealed class ManagementUiContractTests
{
    private static readonly string[] SearchablePageNames =
    [
        "Employees.razor",
        "Departments.razor",
        "LeaveTypes.razor",
        "LeaveBalances.razor",
        "LeaveRequests.razor",
        "LeaveApprovals.razor"
    ];

    public static TheoryData<string, int> SearchablePages => new()
    {
        { "Employees.razor", 6 },
        { "Departments.razor", 2 },
        { "LeaveTypes.razor", 1 },
        { "LeaveBalances.razor", 2 },
        { "LeaveRequests.razor", 2 },
        { "LeaveApprovals.razor", 3 }
    };

    [Theory]
    [MemberData(nameof(SearchablePages))]
    public void SearchableManagementPage_UsesCompleteSharedFilterContract(string fileName, int autocompleteCount)
    {
        var source = ReadRepoFile("Components", "Pages", fileName);

        Assert.Equal(autocompleteCount, CountOccurrences(source, "<MudAutocomplete T=\"string\""));
        Assert.Contains("<ManagementActiveFilters", source);
        Assert.Contains("Filtreleri Temizle", source);
        Assert.Contains("SearchDraft = SearchForm.Clone();", source);
        Assert.Contains("SearchForm = SearchDraft.Normalize();", source);
        Assert.Contains("private async Task RemoveFilterAsync", source);
        Assert.Contains("private async Task ClearFiltersAsync", source);
    }

    [Fact]
    public void EveryManagementTable_UsesSharedScrollableTableClass()
    {
        var pageNames = SearchablePageNames
            .Append("PublicHolidays.razor")
            .Append("AuditLogs.razor");

        foreach (var pageName in pageNames)
        {
            Assert.Contains("Class=\"management-data-table\"", ReadRepoFile("Components", "Pages", pageName));
        }

        var css = ReadRepoFile("wwwroot", "app.css");
        Assert.Contains(".management-data-table .mud-table-container:has(tbody > tr:nth-child(16))", css);
        Assert.Contains("max-height: calc(48px + (15 * 64px));", css);
        Assert.Contains("position: sticky;", css);
    }

    [Fact]
    public void LeaveRequestForm_UsesWorkingDayPreviewExplicitHalfDayAndSubmitConfirmation()
    {
        var source = ReadRepoFile("Components", "Pages", "LeaveRequests.razor");

        Assert.Contains("Label=\"Yarım gün izin\"", source);
        Assert.Contains("Seçilen tarih aralığı", source);
        Assert.Contains("Hafta sonları ve tanımlı resmî tatiller hesaba katılmaz.", source);
        Assert.Contains("Bu talep onaylanırsa seçili çalışanın toplam", source);
        Assert.Contains("LeaveBalanceDashboardSummary.ProjectRemainingDays(totalRemainingDays, requestedDays)", source);
        Assert.Contains("LeaveBalanceDashboardSummary.SumCurrentRemainingDays(group)", source);
        Assert.Contains("EmployeeTotalRemainingDaysById = balances", source);
        Assert.DoesNotContain("LoadSelectedEmployeeTotalRemainingDaysAsync", source);
        Assert.Contains("izin bakiyesinden {FormatDayCount(requestedDays)} iş günü düşülecektir", source);
        Assert.Contains("yesText: IsEditing ? \"Güncelle\" : \"Gönder\"", source);
        Assert.DoesNotContain("Başlangıç Saati", source);
        Assert.DoesNotContain("Bitiş Saati", source);
        Assert.DoesNotContain("InputType.Time", source);
    }

    [Fact]
    public void LeaveRequestForm_UsesOneRangeAuthorityAndDefaultsHalfDayToRangeStart()
    {
        var source = ReadRepoFile("Components", "Pages", "LeaveRequests.razor");

        Assert.Equal(1, CountOccurrences(source, "<MudDateRangePicker"));
        Assert.Contains("@bind-DateRange=\"Form.SelectedDateRange\"", source);
        Assert.Contains("Label=\"İzin Tarih Aralığı\"", source);
        Assert.Contains("Label=\"Yarım Gün Tarihi\"", source);
        Assert.Contains("ValueChanged=\"OnHalfDayChanged\"", source);
        Assert.Contains("Form.SelectedDateRange = new DateRange(startDate, startDate);", source);
        Assert.Contains("Form.SelectedDateRange = new DateRange(date, date);", source);
        Assert.DoesNotContain("@bind-Date=\"Form.StartDate\"", source);
        Assert.DoesNotContain("@bind-Date=\"Form.EndDate\"", source);
    }

    [Fact]
    public void EmployeeFileUi_UsesGeneralInformationAndRelatedRecordSurfaces()
    {
        var dashboard = ReadRepoFile("Components", "Pages", "Home.razor");
        var general = ReadRepoFile("Components", "Pages", "EmployeeGeneralInformation.razor");
        var identityDocuments = ReadRepoFile("Components", "Pages", "EmployeeIdentityDocuments.razor");
        var educations = ReadRepoFile("Components", "Pages", "EmployeeEducations.razor");
        var courses = ReadRepoFile("Components", "Pages", "EmployeeCourseCertificates.razor");
        var relatedFiles = ReadRepoFile("Components", "RelatedDocumentFiles.razor");
        var program = ReadRepoFile("Program.cs");

        Assert.Equal(0, CountOccurrences(dashboard, "<InputFile"));
        Assert.DoesNotContain("UploadProfilePhotoAsync", dashboard);
        Assert.Equal(1, CountOccurrences(general, "<InputFile"));
        Assert.Contains("EmployeeFileService.UploadProfilePhotoAsync", general);
        Assert.Contains("EmployeeFileContentPolicy.MaxProfilePhotoBytes", general);

        Assert.Contains("EmployeeFileService.UploadIdentityDocumentAsync", identityDocuments);
        Assert.Contains("EmployeeFileService.UploadEducationDocumentAsync", educations);
        Assert.Contains("EmployeeFileService.UploadCourseCertificateDocumentAsync", courses);
        Assert.Equal(1, CountOccurrences(relatedFiles, "<InputFile id="));
        Assert.Contains("EmployeeFileContentPolicy.MaxDocumentBytes", identityDocuments);
        Assert.Contains("EmployeeFileContentPolicy.MaxDocumentBytes", educations);
        Assert.Contains("EmployeeFileContentPolicy.MaxDocumentBytes", courses);
        Assert.DoesNotContain("SelectedDocumentCategoryKey", identityDocuments);
        Assert.Contains("app.MapGroup(\"/employee-files\")", program);
        Assert.Contains(".RequireAuthorization()", program);
        Assert.Contains("request.Path.StartsWithSegments(\"/employee-files\")", program);
    }

    [Fact]
    public void EmployeeDeletion_BlocksWhilePersonalFilesExist()
    {
        var source = ReadRepoFile("Components", "Pages", "Employees.razor");

        Assert.Contains("Database.EmployeeProfilePhotos", source);
        Assert.Contains("Database.EmployeeDocuments", source);
        Assert.Contains("profil fotoğrafı veya kişisel belgeleri bulunuyor", source);
        Assert.True(
            source.IndexOf("var hasPersonalFiles", StringComparison.Ordinal)
            < source.IndexOf("Database.Employees.Remove(employee);", StringComparison.Ordinal));
    }

    [Fact]
    public void EmployeeGenderAndBloodGroup_AreControlledEditableSearchableFields()
    {
        var source = ReadRepoFile("Components", "Pages", "Employees.razor");

        Assert.Contains("@bind-Value=\"Form.Gender\"", source);
        Assert.Contains("@bind-Value=\"Form.BloodGroup\"", source);
        Assert.Contains("@bind-Value=\"SearchDraft.Gender\"", source);
        Assert.Contains("@bind-Value=\"SearchDraft.BloodGroup\"", source);
        Assert.Contains("employee.Gender = Form.Gender", source);
        Assert.Contains("employee.BloodGroup = Form.BloodGroup", source);
        Assert.Contains("e.Gender == SearchForm.Gender.Value", source);
        Assert.Contains("e.BloodGroup == SearchForm.BloodGroup.Value", source);
        Assert.Contains("DataLabel=\"Cinsiyet\"", source);
        Assert.Contains("DataLabel=\"Kan Grubu\"", source);
    }

    [Fact]
    public void EmployeeStaffDate_IsIndependentEditableSearchableField()
    {
        var source = ReadRepoFile("Components", "Pages", "Employees.razor");

        Assert.Contains("@bind-Date=\"Form.StartDate\" Label=\"İşe Başlama Tarihi\"", source);
        Assert.Contains("@bind-Date=\"Form.StaffDate\" Label=\"Kadro Tarihi\"", source);
        Assert.Contains("@bind-Date=\"SearchDraft.StaffDate\" Label=\"Kadro Tarihi\"", source);
        Assert.Contains("employee.StaffDate = Form.StaffDate", source);
        Assert.Contains("StaffDate = employee.StaffDate", source);
        Assert.Contains("e.StaffDate >= SearchForm.StaffDate.Value", source);
        Assert.Contains("DataLabel=\"Kadro Tarihi\"", source);
    }

    [Fact]
    public void Departments_DisplayParentDepartmentNameInsteadOfItsIdentifier()
    {
        var source = ReadRepoFile("Components", "Pages", "Departments.razor");

        Assert.Contains("<MudTh>Üst Departman</MudTh>", source);
        Assert.Contains("DataLabel=\"Üst Departman\">@FormatParentDepartment(context.ParentDepartment)", source);
        Assert.Contains(".Include(department => department.ParentDepartment)", source);
        Assert.Contains("parentDepartment?.DepartmentName ?? \"Ana departman\"", source);
        Assert.DoesNotContain("<MudTh>Üst Departman ID</MudTh>", source);
        Assert.DoesNotContain("(ID {dept.DepartmentId})", source);
    }

    [Fact]
    public void ExcelActions_UseOneConsistentAccessibleActionGroup()
    {
        var component = ReadRepoFile("Components", "ExcelImportExportActions.razor");
        var css = ReadRepoFile("wwwroot", "app.css");

        Assert.Contains("role=\"group\" aria-label=\"Excel işlemleri\"", component);
        Assert.Contains("excel-action-button excel-action-button--import", component);
        Assert.Contains("excel-action-button excel-action-button--export", component);
        Assert.Contains("type=\"button\"", component);
        Assert.Contains("disabled=\"@Disabled\"", component);
        Assert.Contains("<span>İçe Aktar</span>", component);
        Assert.Contains("aria-label=\"Excel dosyası içe aktar\"", component);
        Assert.Contains("<span>Dışa Aktar</span>", component);
        Assert.DoesNotContain("Import Excel", component);
        Assert.DoesNotContain("Export Excel", component);
        Assert.Contains(".excel-action-button--import", css);
        Assert.Contains("grid-template-columns: auto minmax(0, 1fr) minmax(0, 1fr);", css);
    }

    private static string ReadRepoFile(params string[] pathSegments)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([repositoryRoot, .. pathSegments]));
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
