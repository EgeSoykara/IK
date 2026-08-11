namespace IK.Web.Services;

public static class PermissionNames
{
    public const string CanManageDepartments = nameof(CanManageDepartments);
    public const string CanviewEmployeeSearch = nameof(CanviewEmployeeSearch);
    public const string CanCreateNewEmployee = nameof(CanCreateNewEmployee);
    public const string CanManageLeaveTypes = nameof(CanManageLeaveTypes);
    public const string CanManagePublicHolidays = nameof(CanManagePublicHolidays);
    public const string CanManageLeaveBalances = nameof(CanManageLeaveBalances);
    public const string CanViewLeaveRequests = nameof(CanViewLeaveRequests);
    public const string CanManageLeaveRequests = nameof(CanManageLeaveRequests);
    public const string CanEditDeleteLeaveRequests = nameof(CanEditDeleteLeaveRequests);
    public const string CanExectuteApproveLeave = nameof(CanExectuteApproveLeave);
    public const string CanActAsHumanResources = nameof(CanActAsHumanResources);
    public const string CanViewAuditLogs = nameof(CanViewAuditLogs);
    public const string CanViewAllPersonnelInformation = nameof(CanViewAllPersonnelInformation);
    public const string CanEditAllPersonnelInformation = nameof(CanEditAllPersonnelInformation);
    public const string CanAccessSensitivePersonnelInformation = nameof(CanAccessSensitivePersonnelInformation);
    public const string CanDownloadPersonnelDocuments = nameof(CanDownloadPersonnelDocuments);

    public static readonly IReadOnlyList<string> All =
    [
        CanManageDepartments,
        CanviewEmployeeSearch,
        CanCreateNewEmployee,
        CanManageLeaveTypes,
        CanManagePublicHolidays,
        CanManageLeaveBalances,
        CanViewLeaveRequests,
        CanManageLeaveRequests,
        CanEditDeleteLeaveRequests,
        CanExectuteApproveLeave,
        CanActAsHumanResources,
        CanViewAuditLogs,
        CanViewAllPersonnelInformation,
        CanEditAllPersonnelInformation,
        CanAccessSensitivePersonnelInformation,
        CanDownloadPersonnelDocuments
    ];
}
