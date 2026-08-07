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
        "LeaveApprovals.razor",
        "LeaveCarryOverWarnings.razor",
        "EmployeeTerminations.razor"
    ];

    public static TheoryData<string, int> SearchablePages => new()
    {
        { "Employees.razor", 6 },
        { "Departments.razor", 2 },
        { "LeaveTypes.razor", 1 },
        { "LeaveBalances.razor", 3 },
        { "LeaveRequests.razor", 1 },
        { "LeaveApprovals.razor", 3 },
        { "LeaveCarryOverWarnings.razor", 2 },
        { "EmployeeTerminations.razor", 3 }
    };

    [Theory]
    [MemberData(nameof(SearchablePages))]
    public void SearchableManagementPage_UsesCompleteSharedFilterContract(string fileName, int autocompleteCount)
    {
        var source = ReadRepoFile("Components", "Pages", fileName);

        Assert.Equal(autocompleteCount, CountOccurrences(source, "<MudAutocomplete T=\"string\""));
        Assert.Equal(1, CountOccurrences(source, "<ManagementFilterButton"));
        Assert.DoesNotContain("Icons.Material.Filled.Search", source);
        Assert.Contains("<ManagementActiveFilters", source);
        Assert.Contains("Filtreleri Temizle", source);
        Assert.Contains("SearchDraft = SearchForm.Clone();", source);
        Assert.Contains("SearchForm = SearchDraft.Normalize();", source);
        Assert.Contains("private async Task RemoveFilterAsync", source);
        Assert.Contains("private async Task ClearFiltersAsync", source);
    }

    [Fact]
    public void ManagementFilterButton_UsesApprovedLeaveApprovalFormat()
    {
        var source = ReadRepoFile("Components", "ManagementFilterButton.razor");

        Assert.Contains("Variant=\"Variant.Outlined\"", source);
        Assert.Contains("Color=\"Color.Primary\"", source);
        Assert.Contains("StartIcon=\"@Icons.Material.Filled.FilterList\"", source);
        Assert.Contains("aria-label=\"@AriaLabel\"", source);
        Assert.Contains("Filtrele", source);
    }

    [Fact]
    public void EveryFilterButton_IsBelowPageHeaderAndOutsideTableOrExcelToolbars()
    {
        foreach (var pageName in SearchablePageNames.Append("AuditLogs.razor"))
        {
            var source = ReadRepoFile("Components", "Pages", pageName);
            var headerEnd = source.IndexOf("</section>", StringComparison.Ordinal);
            var filterIndex = source.IndexOf("<ManagementFilterButton", StringComparison.Ordinal);
            var firstTable = source.IndexOf("<MudTable", StringComparison.Ordinal);

            Assert.True(filterIndex > headerEnd, $"{pageName}: filtre başlığın altında olmalı.");
            Assert.True(firstTable < 0 || filterIndex < firstTable, $"{pageName}: filtre tablonun üstünde olmalı.");
            foreach (var toolbar in ExtractBlocks(source, "<ToolBarContent>", "</ToolBarContent>"))
            {
                Assert.DoesNotContain("ManagementFilterButton", toolbar);
            }
        }
    }

    [Fact]
    public void ManagementCreateButton_IsTheSingleCreateActionFormat()
    {
        var component = ReadRepoFile("Components", "ManagementCreateButton.razor");
        var css = ReadRepoFile("wwwroot", "app.css");
        Assert.Contains("Variant=\"Variant.Filled\"", component);
        Assert.Contains("Color=\"Color.Primary\"", component);
        Assert.Contains("StartIcon=\"@Icons.Material.Filled.Add\"", component);
        Assert.Contains("Class=\"management-create-button\"", component);
        var createButtonStyle = CssBlock(css, ".management-create-button.mud-button-root {");
        Assert.Contains("min-height: 44px;", createButtonStyle);
        Assert.Contains("background: var(--ik-primary) !important;", createButtonStyle);
        Assert.Contains("border-radius: 8px;", createButtonStyle);

        string[] pagesWithCreateActions =
        [
            "Departments.razor",
            "Employees.razor",
            "LeaveBalances.razor",
            "LeaveRequests.razor",
            "LeaveTypes.razor",
            "PublicHolidays.razor",
            "EmployeeAddresses.razor",
            "EmployeeBankAccounts.razor",
            "EmployeeCourseCertificates.razor",
            "EmployeeEducations.razor",
            "EmployeeIdentityDocuments.razor",
            "EmployeePhones.razor",
            "EmployeeTerminations.razor"
        ];

        foreach (var pageName in pagesWithCreateActions)
        {
            var source = ReadRepoFile("Components", "Pages", pageName);
            Assert.Contains("<ManagementCreateButton", source);
            Assert.DoesNotContain("<MudFab", source);
            Assert.DoesNotContain("Icons.Material.Filled.Add", source);
        }
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
        Assert.Contains("max-height: calc(48px + (5 * 64px));", css);
        Assert.Contains("position: sticky;", css);
    }

    [Fact]
    public void LeaveTypes_EnforcesMobilizationPolicyBeforePersistence()
    {
        var source = ReadRepoFile("Components", "Pages", "LeaveTypes.razor");

        Assert.Contains("LeaveEntitlementKind.MaleEmployees", source);
        Assert.Contains("DomainConstants.MobilizationLeaveMaximumDays", source);
        Assert.Contains("Seferberlik İzni devredemez", source);
    }

    [Fact]
    public void CarryOverWarnings_UseSingleReviewDecisionAndTrueClearAll()
    {
        var source = ReadRepoFile("Components", "Pages", "LeaveCarryOverWarnings.razor");

        Assert.Contains("Devir Uyarısını İncele", source);
        Assert.Contains("Label=\"Devreden Gün\"", source);
        Assert.Contains("Step=\"0.5m\"", source);
        Assert.Contains("İncelemeyi Onayla", source);
        Assert.Contains("ReviewAsync", source);
        Assert.Contains("OnClick=\"@(() => OpenReviewDialog(context))\"", source);
        Assert.DoesNotContain("UpdateCarryOverDaysAsync", source);
        Assert.DoesNotContain("AcknowledgeAsync", source);
        Assert.DoesNotContain(">Düzenle<", source);
        Assert.DoesNotContain("İncelendi İşaretle", source);
        Assert.Contains("Yeni toplam", source);
        Assert.Contains("Yeni kalan", source);
        Assert.Contains("SearchForm = LeaveCarryOverWarningSearchForm.CreateEmpty();", source);
        Assert.Contains("public static LeaveCarryOverWarningSearchForm CreateEmpty() => new();", source);
    }

    [Fact]
    public void KoopbankTheme_IsTheSingleShellAndDashboardVisualAuthority()
    {
        var theme = ReadRepoFile("Components", "Layout", "StitchTheme.cs");
        var app = ReadRepoFile("Components", "App.razor");
        var mainLayout = ReadRepoFile("Components", "Layout", "MainLayout.razor");
        var loginLayout = ReadRepoFile("Components", "Layout", "LoginLayout.razor");
        var dashboard = ReadRepoFile("Components", "Pages", "Home.razor");
        var css = ReadRepoFile("wwwroot", "app.css");

        Assert.Contains("Primary = \"#c8102e\"", theme);
        Assert.Contains("Secondary = \"#111111\"", theme);
        Assert.Contains("DrawerBackground = \"#111111\"", theme);
        Assert.Contains("Success = \"#19764a\"", theme);
        Assert.DoesNotContain("#3f4ad4", theme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("KOOPBANK · İnsan Kaynakları", mainLayout);
        Assert.Contains("KOOPBANK · İnsan Kaynakları", loginLayout);
        Assert.Contains("href=\"@Assets[\"favicon.ico\"]\"", app);
        Assert.DoesNotContain("favicon.png", app);
        Assert.Contains("class=\"app-shell-brand\"", mainLayout);
        Assert.Contains("src=\"@Assets[\"koopbank_400x400.jpg\"]\"", mainLayout);
        Assert.Contains("aria-label=\"Menüyü aç/kapat\"", mainLayout);
        Assert.Contains("KOOPBANK · İK Operasyon Kontrol Merkezi", dashboard);
        Assert.Equal(4, CountOccurrences(dashboard, "class=\"dashboard-stat-icon\""));
        Assert.Contains("GetRequestStatusClass(request.Status)", dashboard);
        Assert.Contains("--ik-primary: #c8102e;", css);
        Assert.Contains("--ik-on-primary-container: #ffffff;", css);
        Assert.Contains("--ik-on-warning-surface: #754600;", css);
        Assert.Contains("--ik-secondary: #111111;", css);
        var heroStyleStart = css.IndexOf(".dashboard-hero {", StringComparison.Ordinal);
        Assert.True(heroStyleStart >= 0);
        var heroStyleEnd = css.IndexOf('}', heroStyleStart);
        Assert.True(heroStyleEnd > heroStyleStart);
        var heroStyle = css[heroStyleStart..heroStyleEnd];
        Assert.Contains("min-height: 196px;", heroStyle);
        Assert.Contains("color: var(--ik-on-surface);", heroStyle);
        Assert.Contains("background: var(--ik-surface);", heroStyle);
        Assert.Contains("border-left: 4px solid var(--ik-primary);", heroStyle);
        var heroParagraphStyleStart = css.IndexOf(".dashboard-hero p {", StringComparison.Ordinal);
        Assert.True(heroParagraphStyleStart >= 0);
        var heroParagraphStyleEnd = css.IndexOf('}', heroParagraphStyleStart);
        Assert.True(heroParagraphStyleEnd > heroParagraphStyleStart);
        var heroParagraphStyle = css[heroParagraphStyleStart..heroParagraphStyleEnd];
        Assert.Contains("color: var(--ik-on-surface-variant);", heroParagraphStyle);
        Assert.DoesNotContain(".dashboard-hero::after", css);
        Assert.DoesNotContain(".dashboard-hero > *", css);
        var dashboardChipStyleStart = css.IndexOf(".dashboard-chip {", StringComparison.Ordinal);
        Assert.True(dashboardChipStyleStart >= 0);
        var dashboardChipStyleEnd = css.IndexOf('}', dashboardChipStyleStart);
        Assert.True(dashboardChipStyleEnd > dashboardChipStyleStart);
        var dashboardChipStyle = css[dashboardChipStyleStart..dashboardChipStyleEnd];
        Assert.Contains("min-height: 30px;", dashboardChipStyle);
        Assert.Contains("font-size: 0.75rem;", dashboardChipStyle);
        Assert.Contains("line-height: 1.25;", dashboardChipStyle);
        Assert.Contains(
            """
            .leave-calendar-today > header > span {
                background: var(--ik-primary);
                color: var(--ik-on-primary);
            }
            """,
            css);

        string[] supersededTokens =
        [
            "#3f4ad4",
            "#001529",
            "#5a65ee",
            "#005daa",
            "#4d6077",
            "#0075d5",
            "#bec2ff",
            "#4f46e5",
            "#475569",
            "#0f172a",
            "#64748b",
            "#334155",
            "#94a3b8",
            "#e2e8f0",
            "#f8fafc"
        ];

        foreach (var supersededToken in supersededTokens)
        {
            Assert.DoesNotContain(supersededToken, theme, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(supersededToken, css, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Dashboard_UsesBoundedCurrentDataAndLinksToThePagedRequestAuthority()
    {
        var dashboard = ReadRepoFile("Components", "Pages", "Home.razor");
        var query = ReadRepoFile("Services", "DashboardPageQuery.cs");
        var requests = ReadRepoFile("Components", "Pages", "LeaveRequests.razor");

        Assert.Contains("PendingRequestPreviewLimit = 3", query);
        Assert.Contains(".Take(PendingRequestPreviewLimit)", query);
        Assert.Contains("PendingCount", query);
        Assert.Contains("CompletedCount", query);
        Assert.Contains("LatestCompletedRequestDate", query);
        Assert.Contains("DashboardPageQuery.LoadCurrentBalancesAsync", dashboard);
        Assert.Contains("DashboardPageQuery.LoadPendingItemsAsync", dashboard);
        Assert.Contains("request.IsCancellation", dashboard);
        Assert.Contains("LeaveCancellationRequests", query);
        Assert.Contains("Tüm Talepleri Görüntüle", dashboard);
        Assert.Contains("LeaveRequestPageLink.ForEmployee", dashboard);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"employeeId\")]", requests);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"create\")]", requests);
        Assert.Contains("OpenLeaveRequestEditorForEmployee(requestedEmployee)", requests);
        Assert.Contains("query.Where(r => r.EmployeeId == SearchForm.EmployeeId.Value)", requests);
        Assert.DoesNotContain("SelectedEmployeeCompletedLeaveRequests", dashboard);
        Assert.DoesNotContain("Tamamlanan izin talebi bulunamadı.", dashboard);
    }

    [Fact]
    public void NestedNavigationAndRowActions_HaveDistinctVisualHierarchy()
    {
        var css = ReadRepoFile("wwwroot", "app.css");
        var theme = ReadRepoFile("Components", "Layout", "StitchTheme.cs");

        Assert.Contains("Info = \"#1d4ed8\"", theme);
        Assert.Contains("--ik-edit: #1d4ed8;", css);
        Assert.Contains(
            ".app-nav-menu .mud-nav-group > .mud-navgroup-collapse .mud-navmenu",
            css);
        Assert.Contains("border-left: 1px solid rgba(255, 255, 255, 0.24);", css);
        Assert.Contains(
            ".app-nav-menu .mud-nav-group > .mud-navgroup-collapse .mud-nav-item::before",
            css);
        Assert.Contains(
            ".management-data-table .mud-icon-button.mud-button-outlined-info",
            css);

        string[] pagesWithEditDeletePairs =
        [
            "Departments.razor",
            "Employees.razor",
            "LeaveBalances.razor",
            "LeaveRequests.razor",
            "LeaveTypes.razor",
            "PublicHolidays.razor",
            "EmployeeAddresses.razor",
            "EmployeeBankAccounts.razor",
            "EmployeeCourseCertificates.razor",
            "EmployeeEducations.razor",
            "EmployeeIdentityDocuments.razor",
            "EmployeePhones.razor",
            "EmployeeTerminations.razor"
        ];

        foreach (var pageName in pagesWithEditDeletePairs)
        {
            var source = ReadRepoFile("Components", "Pages", pageName);
            Assert.Contains("Color=\"Color.Info\"", source);
            Assert.Contains("Color=\"Color.Error\"", source);
        }
    }

    [Fact]
    public void LeaveApprovals_UseApprovedStitchQueueAndSingleDecisionFlow()
    {
        var source = ReadRepoFile("Components", "Pages", "LeaveApprovals.razor");
        var css = ReadRepoFile("wwwroot", "app.css");

        Assert.Contains("id=\"incoming-approvals-title\">Gelen Talepler", source);
        Assert.Contains("id=\"approval-history-title\">Karar Geçmişi", source);
        Assert.Contains("@PendingApprovalCount bekleyen", source);
        Assert.Contains("ServerData=\"LoadIncomingApprovalsAsync\"", source);
        Assert.Contains("ServerData=\"LoadDecisionHistoryAsync\"", source);
        Assert.Contains("IDbContextFactory<HumanResourcesDbContext>", source);
        Assert.Contains("OpenCommentDialog(context.Request.Reason)", source);
        Assert.Contains("OpenLeaveApprovalDialog(context)", source);
        Assert.Contains("İzin İptal Talepleri", source);
        Assert.Contains("LoadCancellationApprovalsAsync", source);
        Assert.Contains("ApplyCancellationApprovalSearch", source);
        Assert.Contains("OpenCancellationApprovalDialog(context)", source);
        Assert.DoesNotContain("@bind-Value=\"Form.Decision\"", source);
        Assert.Contains("SelectDecision(LeaveApprovalDecision.Approved)", source);
        Assert.Contains("SelectDecision(LeaveApprovalDecision.Rejected)", source);
        Assert.Contains("Onay Kararını Kaydet", source);
        Assert.Contains("Ret Kararını Kaydet", source);
        Assert.Contains("Reddedilen onaylarda gerekçe belirtilmelidir.", source);
        Assert.Contains("leave-approval-request-summary", source);
        var historySectionStart = source.IndexOf(
            "<section class=\"leave-approval-section leave-approval-history\"",
            StringComparison.Ordinal);
        Assert.True(historySectionStart >= 0);
        var incomingSection = source[..historySectionStart];
        var historySection = source[historySectionStart..];
        Assert.DoesNotContain("DeleteLeaveApprovalAsync", incomingSection);
        Assert.DoesNotContain("DeleteLeaveApprovalAsync", historySection);
        Assert.DoesNotContain("Onay {context.ApprovalId} kaydını sil", historySection);
        Assert.DoesNotContain("<MudTh>İşlemler</MudTh>", historySection);
        Assert.Contains("min-height: 44px;", css);
        Assert.Contains(".leave-approval-decision-options", css);
        Assert.Contains(".leave-approval-status-approved", css);
        Assert.Contains(".leave-approval-status-rejected", css);
    }

    [Fact]
    public void PublicHolidayYearNavigation_StaysInOneCompactGroup()
    {
        var source = ReadRepoFile("Components", "Pages", "PublicHolidays.razor");
        var css = ReadRepoFile("wwwroot", "app.css");
        var groupStart = source.IndexOf(
            "<div class=\"public-holiday-year-navigation\">",
            StringComparison.Ordinal);
        Assert.True(groupStart >= 0);

        var groupEnd = source.IndexOf("</div>", groupStart, StringComparison.Ordinal);
        Assert.True(groupEnd > groupStart);
        Assert.InRange(source.IndexOf("aria-label=\"Önceki yıl\"", StringComparison.Ordinal), groupStart, groupEnd);
        Assert.InRange(source.IndexOf("Label=\"Takvim yılı\"", StringComparison.Ordinal), groupStart, groupEnd);
        Assert.InRange(source.IndexOf("aria-label=\"Sonraki yıl\"", StringComparison.Ordinal), groupStart, groupEnd);
        Assert.True(source.IndexOf("<MudSpacer />", StringComparison.Ordinal) > groupEnd);
        Assert.Contains(
            """
            .public-holiday-year-navigation {
                align-items: center;
                display: flex;
                gap: 0.5rem;
            }
            """,
            css);
    }

    [Fact]
    public void LeaveRequestForm_UsesWorkingDayPreviewExplicitHalfDayAndSubmitConfirmation()
    {
        var source = ReadRepoFile("Components", "Pages", "LeaveRequests.razor");

        Assert.Contains("Label=\"Yarım gün izin\"", source);
        Assert.Contains("Seçilen tarih aralığı", source);
        Assert.Contains("Hafta sonları ve tanımlı resmî tatiller hesaba katılmaz.", source);
        Assert.Contains("seçili çalışanın @Form.LeaveOptionName bakiyesi", source);
        Assert.Contains("LeaveBalanceDashboardSummary.ProjectRemainingDays(totalRemainingDays, requestedDays)", source);
        Assert.Contains("LoadAvailableLeaveRequestOptionsAsync", source);
        Assert.Contains("balance.RemainingDays > 0m", source);
        Assert.Contains("Label=\"İzin Türü\"", source);
        Assert.Contains("LeaveRequestCategory.AnnualLeave", source);
        Assert.Contains("LeaveRequestCategory.SpecificLeaveType", source);
        Assert.Contains("leaveTypeId: Form.LeaveTypeId", source);
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
    public void LeaveRequests_UseApprovedListAndSinglePopupEditorAuthority()
    {
        var source = ReadRepoFile("Components", "Pages", "LeaveRequests.razor");
        var css = ReadRepoFile("wwwroot", "app.css");

        Assert.Contains("class=\"management-page-header leave-request-page-header\"", source);
        Assert.Contains("class=\"leave-request-list-workspace\"", source);
        Assert.Contains("class=\"leave-request-status-tabs\"", source);
        Assert.Contains("SelectStatusFilterAsync", source);
        Assert.Contains("SearchForm.Status = status;", source);
        Assert.Contains("aria-pressed=\"@(SearchForm.Status is null ? \"true\" : \"false\")\"", source);
        Assert.Contains("aria-pressed=\"@(SearchForm.Status == status ? \"true\" : \"false\")\"", source);
        Assert.Contains("class=\"leave-request-editor-layout\"", source);
        Assert.Contains("class=\"leave-request-summary\"", source);
        Assert.Contains("class=\"leave-request-summary-column\"", source);
        Assert.Contains("class=\"leave-request-owner-grid\"", source);
        Assert.Contains("class=\"leave-request-half-day-panel\"", source);
        Assert.Contains("@(IsEditing ? \"Talebi Düzenle\" : \"Yeni Talep\")", source);
        Assert.Contains("@(IsEditing ? \"İzin Talebini Düzenle\" : \"İzin Talebi Oluştur\")", source);
        Assert.Contains("Icon=\"@Icons.Material.Filled.Person\"", source);
        Assert.Contains("Icon=\"@Icons.Material.Filled.Event\"", source);
        Assert.Contains("Icon=\"@Icons.Material.Filled.Description\"", source);
        Assert.Contains("EndIcon=\"@Icons.Material.Filled.Send\"", source);
        Assert.Contains("RequiredError=\"Çalışan seçimi zorunludur.\"", source);
        Assert.Equal(2, CountOccurrences(source, "RequiredError=\"Başlangıç ve bitiş tarihleri zorunludur.\""));
        Assert.Contains("RequiredError=\"İzin talep nedeni zorunludur.\"", source);
        Assert.Contains("<dt>Tarih Aralığı</dt>", source);
        Assert.Contains("<dt>İş Günü Sayısı</dt>", source);
        Assert.Contains("<dt>Kalan Bakiye</dt>", source);
        Assert.Contains("Talep Sahibi", source);
        Assert.Contains("İzin Bilgileri", source);
        Assert.Contains("Talep Özeti", source);
        Assert.Contains("OpenLeaveRequestEditor", source);
        Assert.Contains("CloseLeaveRequestEditor", source);
        Assert.Equal(2, CountOccurrences(source, "<EditForm"));
        Assert.Contains("<MudDialog @bind-Visible=\"IsLeaveRequestEditorOpen\" Options=\"LeaveRequestDialogOptions\">", source);
        Assert.Contains("MaxWidth = MaxWidth.ExtraLarge", source);
        Assert.Contains("BackdropClick = false", source);
        Assert.DoesNotContain("İzin Taleplerine Dön", source);
        Assert.DoesNotContain("<MudFab", source);
        Assert.Contains(".leave-request-editor-layout", css);
        Assert.Contains("grid-template-columns: minmax(0, 2fr) minmax(280px, 1fr);", css);
        Assert.Contains("grid-template-columns: minmax(0, 1fr) minmax(0, 2fr);", css);
        Assert.Contains(".leave-request-status-tab-active", css);
        Assert.DoesNotContain(".leave-request-editor-header > .mud-button-root", css);
        Assert.Contains("Geriye dönük izin talebi", source);
        Assert.Contains("IsRetrospective", source);
        Assert.Contains("İzin İptal Talebi", source);
        Assert.Contains("CancelPendingRequestAsync", source);
        Assert.Contains("CreateCancellationAsync", source);
        Assert.Contains("Label=\"İptal Başlangıç Tarihi\"", source);
        Assert.Contains("Label=\"İptal Bitiş Tarihi\"", source);
        Assert.Contains("İşe başlayacağı tarih:", source);
        Assert.Contains("RequestStatusClass(context)", source);
        Assert.Contains("FormatDisplayedRequestStatus(context)", source);
        Assert.Contains("HasPendingCancellation(request) ? \"İptal sürecinde\"", source);
        Assert.Contains("İptal talebi · Müdür onayı bekleniyor", source);
        Assert.Contains("İptal talebi · İK onayı bekleniyor", source);
        Assert.DoesNotContain("SubmitCancellationDecisionAsync", source);
        var approvalSource = ReadRepoFile("Components", "Pages", "LeaveApprovals.razor");
        Assert.Contains("LeaveCancellationService.ManagerDecisionAsync", approvalSource);
        Assert.Contains("LeaveCancellationService.HumanResourcesDecisionAsync", approvalSource);
        Assert.Contains("ServerData=\"LoadCancellationHistoryAsync\"", approvalSource);
        Assert.Contains("İptal Kararları", approvalSource);
        Assert.Contains("<section class=\"leave-approval-section leave-cancellation-history\"", approvalSource);
        Assert.Contains("aria-labelledby=\"cancellation-history-title\"", approvalSource);
        Assert.DoesNotContain("leave-cancellation-history-heading", approvalSource);
        Assert.Contains("request.CurrentStatus == LeaveRequestStatus.Approved && !request.IsDirectCancellation", approvalSource);
        Assert.Contains("Include(item => item.RequestedByEmployee)", approvalSource);
    }

    [Fact]
    public void LeaveDayInputsAndTables_HideUnnecessaryDecimalZeros()
    {
        var balances = ReadRepoFile("Components", "Pages", "LeaveBalances.razor");
        var leaveTypes = ReadRepoFile("Components", "Pages", "LeaveTypes.razor");
        var requests = ReadRepoFile("Components", "Pages", "LeaveRequests.razor");
        var warnings = ReadRepoFile("Components", "Pages", "LeaveCarryOverWarnings.razor");

        Assert.Equal(3, CountOccurrences(balances, "Format=\"0.##\""));
        Assert.Equal(2, CountOccurrences(leaveTypes, "Format=\"0.##\""));
        Assert.Equal(1, CountOccurrences(warnings, "Format=\"0.##\""));
        Assert.Contains("FormatDayCount(context.AnnualQuota)", leaveTypes);
        Assert.Contains("FormatDayCount(context.MaxAccrualDays)", leaveTypes);
        Assert.Contains("FormatDayCount(context.RequestedDays)", requests);
    }

    [Fact]
    public void LeaveBalanceManagement_UsesBulkScopesDepartmentAutocompleteAndServiceMutations()
    {
        var source = ReadRepoFile("Components", "Pages", "LeaveBalances.razor");

        Assert.Contains("LeaveBalanceTargetScope.Employee", source);
        Assert.Contains("LeaveBalanceTargetScope.Department", source);
        Assert.Contains("LeaveBalanceTargetScope.AllEmployees", source);
        Assert.Contains("Label=\"Departman\"", source);
        Assert.Contains("SearchDepartmentNamesAsync", source);
        Assert.Contains("LeaveBalanceService.AssignManualAsync", source);
        Assert.Contains("LeaveBalanceService.UpdateAsync", source);
        Assert.Contains("LeaveBalanceService.DeleteAsync", source);
        Assert.Contains("Label=\"Kullanılan Gün\"", source);
        Assert.Contains("Form.UsedDays", source);
        Assert.Contains("UsedDays = balance.UsedDays", source);
        Assert.Equal(3, CountOccurrences(source, "Step=\"0.5m\""));
        Assert.DoesNotContain("<MudTh>Bakiye ID</MudTh>", source);
        Assert.DoesNotContain("Label=\"Bakiye ID\"", source);
        Assert.DoesNotContain("Database.LeaveBalances.Remove", source);
    }

    [Fact]
    public void LeaveBalanceManagement_GroupsEmployeesWithIndependentExpandableBalanceDetails()
    {
        var source = ReadRepoFile("Components", "Pages", "LeaveBalances.razor");
        var css = ReadRepoFile("wwwroot", "app.css");

        Assert.Contains("MudTable<LeaveBalanceEmployeeGroup>", source);
        Assert.Contains("<ChildRowContent>", source);
        Assert.Contains("HashSet<int> ExpandedEmployeeIds", source);
        Assert.Contains("ToggleEmployee(context.Employee.EmployeeId)", source);
        Assert.Contains("aria-expanded=\"@(IsEmployeeExpanded(context.Employee.EmployeeId) ? \"true\" : \"false\")\"", source);
        Assert.Contains("Çalışanın filtrelere uyan izin bakiyeleri", source);
        Assert.Contains("matchingEmployeeIds", source);
        Assert.Contains("TotalItems = Math.Max(employeeGroups.Count, totalEmployees - missingEmployeeCount)", source);
        Assert.Contains(".Where(balancesByEmployee.ContainsKey)", source);
        Assert.Contains("totalEmployees - missingEmployeeCount", source);
        Assert.Contains("LeaveBalanceDashboardSummary.SumCurrentRemainingDays(Balances)", source);
        Assert.Contains("<table>", source);
        Assert.Contains("<th scope=\"col\">İzin Türü</th>", source);
        Assert.Contains("<th scope=\"row\"", source);
        Assert.Contains("aria-label=\"@($\"Kalan: {FormatDayCount(balance.RemainingDays)}\")\"", source);
        Assert.Contains("leave-balance-detail-actions", source);
        Assert.DoesNotContain("<MudTh>İzin Türü</MudTh>", source);
        Assert.Contains(".leave-balance-employee-toggle", css);
        Assert.Contains(".leave-balance-detail-item", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr));", css);
    }

    [Fact]
    public void AuditLogPage_HidesEntityIdentifiersAndShowsOperationalDetails()
    {
        var source = ReadRepoFile("Components", "Pages", "AuditLogs.razor");

        Assert.DoesNotContain("<MudTh>Varlık Adı</MudTh>", source);
        Assert.DoesNotContain("<MudTh>Varlık ID</MudTh>", source);
        Assert.DoesNotContain("FormatEntityName", source);
        Assert.Contains("<MudTh>Detay</MudTh>", source);
        Assert.Contains("AuditLogPresentation.FormatDetails(SelectedAuditLog)", source);
        Assert.Contains("BuildDetailPreview(context)", source);
        Assert.Contains("Denetim Kaydı Detayı", source);
        Assert.Contains("İşlemi yapan", source);
        Assert.Contains("Yapılan işlem", source);
        Assert.Contains("İşlem zamanı", source);
        Assert.Contains("İşlem açıklaması", source);
        Assert.Contains("<MudAutocomplete T=\"AuditActorOption\"", source);
        Assert.Contains("SearchFunc=\"SearchAuditEmployeesAsync\"", source);
        Assert.Contains("Actor?.EmployeeId", source);
        Assert.Contains("CoerceValue=\"false\"", source);
        Assert.Contains("SetQuickDateRange(7)", source);
        Assert.Contains("HasSearched", source);
        Assert.Contains("AuditLogSearchCriteria", source);
        Assert.Equal(1, CountOccurrences(source, "Immediate=\"true\""));
        Assert.Contains("CultureInfo.GetCultureInfo(\"tr-TR\")", source);
    }

    [Fact]
    public void Program_UsesTurkishCultureForEveryCalendarAndRegistersDailyWorker()
    {
        var source = ReadRepoFile("Program.cs");

        Assert.Contains("CultureInfo.GetCultureInfo(\"tr-TR\")", source);
        Assert.Contains("DefaultThreadCurrentCulture", source);
        Assert.Contains("DefaultThreadCurrentUICulture", source);
        Assert.Contains("app.UseRequestLocalization();", source);
        Assert.Contains("AddHostedService<DailyLeaveEntitlementWorker>()", source);
    }

    [Fact]
    public void EmployeeFileUi_UsesCombinedPersonnelInformationAndRelatedRecordSurfaces()
    {
        var dashboard = ReadRepoFile("Components", "Pages", "Home.razor");
        var personnel = ReadRepoFile("Components", "Pages", "EmployeePersonnelInformation.razor");
        var identityDocuments = ReadRepoFile("Components", "Pages", "EmployeeIdentityDocuments.razor");
        var educations = ReadRepoFile("Components", "Pages", "EmployeeEducations.razor");
        var courses = ReadRepoFile("Components", "Pages", "EmployeeCourseCertificates.razor");
        var relatedFiles = ReadRepoFile("Components", "RelatedDocumentFiles.razor");
        var program = ReadRepoFile("Program.cs");

        Assert.Equal(0, CountOccurrences(dashboard, "<InputFile"));
        Assert.DoesNotContain("UploadProfilePhotoAsync", dashboard);
        Assert.Equal(1, CountOccurrences(personnel, "<InputFile"));
        Assert.Contains("EmployeeFileService.UploadProfilePhotoAsync", personnel);
        Assert.Contains("EmployeeFileContentPolicy.MaxProfilePhotoBytes", personnel);
        Assert.Contains("Organizasyon Bilgileri", personnel);

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

    private static string CssBlock(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(start >= 0, $"CSS selector not found: {selector}");
        var end = css.IndexOf('}', start);
        Assert.True(end > start, $"CSS block is incomplete: {selector}");
        return css[start..(end + 1)];
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static IEnumerable<string> ExtractBlocks(
        string source,
        string startToken,
        string endToken)
    {
        var offset = 0;
        while (true)
        {
            var start = source.IndexOf(startToken, offset, StringComparison.Ordinal);
            if (start < 0) yield break;
            var end = source.IndexOf(endToken, start, StringComparison.Ordinal);
            Assert.True(end >= 0, $"Block is missing closing token: {endToken}");
            yield return source[start..(end + endToken.Length)];
            offset = end + endToken.Length;
        }
    }
}
