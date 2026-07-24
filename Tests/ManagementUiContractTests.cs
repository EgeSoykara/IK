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
        var pageNames = SearchablePageNames.Append("AuditLogs.razor");

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
        Assert.Contains("Hafta sonları hesaba katılmaz.", source);
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
    public void Dashboard_UsesOwnEmployeeFileAuthorityAndCategoryCanonicalKeys()
    {
        var source = ReadRepoFile("Components", "Pages", "Home.razor");
        var program = ReadRepoFile("Program.cs");

        Assert.Equal(2, CountOccurrences(source, "<InputFile"));
        Assert.Contains("IsOwnDashboard", source);
        Assert.Contains("EmployeeFileService.UploadMyProfilePhotoAsync", source);
        Assert.Contains("EmployeeFileService.UploadMyDocumentAsync", source);
        Assert.Contains("SelectedDocumentCategoryKey", source);
        Assert.Contains("data-category-key=\"@group.Category.CanonicalKey\"", source);
        Assert.Contains("EmployeeFileContentPolicy.MaxProfilePhotoBytes", source);
        Assert.Contains("EmployeeFileContentPolicy.MaxDocumentBytes", source);
        Assert.Contains(".GroupBy(document => document.CategoryCanonicalKey)", source);
        Assert.Contains("disabled=\"@IsFileOperationInProgress\"", source);
        Assert.Contains("Disabled=\"@IsFileOperationInProgress\"", source);
        Assert.Contains(
            "disabled=\"@(IsFileOperationInProgress || DocumentCategories.Count == 0)\"",
            source);
        Assert.Equal(
            2,
            CountOccurrences(source, "if (IsFileOperationInProgress)"));
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

    private static string ReadRepoFile(params string[] pathSegments)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([repositoryRoot, .. pathSegments]));
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
