namespace IK.Web.Services;

public sealed class StaticPermissionService
{
    private static readonly IReadOnlyDictionary<StaticApplicationRole, string[]> PermissionsByRole =
        new Dictionary<StaticApplicationRole, string[]>
        {
            [StaticApplicationRole.Employee] =
            [
                PermissionNames.CanManageLeaveRequests
            ],
            [StaticApplicationRole.Administrator] =
            [
                PermissionNames.CanManageDepartments,
                PermissionNames.CanviewEmployeeSearch,
                PermissionNames.CanCreateNewEmployee,
                PermissionNames.CanManageLeaveTypes,
                PermissionNames.CanManageLeaveBalances,
                PermissionNames.CanViewLeaveRequests,
                PermissionNames.CanManageLeaveRequests,
                PermissionNames.CanEditDeleteLeaveRequests,
                PermissionNames.CanExectuteApproveLeave,
                PermissionNames.CanViewAuditLogs
            ],
            [StaticApplicationRole.HumanResources] =
                [
                    PermissionNames.CanManageDepartments,
                    PermissionNames.CanviewEmployeeSearch,
                    PermissionNames.CanCreateNewEmployee,
                    PermissionNames.CanManageLeaveTypes,
                    PermissionNames.CanManageLeaveBalances,
                    PermissionNames.CanViewLeaveRequests,
                    PermissionNames.CanManageLeaveRequests,
                    PermissionNames.CanEditDeleteLeaveRequests,
                    PermissionNames.CanExectuteApproveLeave,
                    PermissionNames.CanViewAuditLogs
                ]
        };

    private static readonly IReadOnlyDictionary<string, StaticUserPermissionOverride> PermissionOverridesByUser =
        new Dictionary<string, StaticUserPermissionOverride>(StringComparer.OrdinalIgnoreCase)
        // {
        //     ["user"] = new(
        //         AddedPermissions:
        //         [
        //             PermissionNames.CanviewEmployeeSearch,
        //             PermissionNames.CanViewLeaveRequests,
        //             PermissionNames.CanEditDeleteLeaveRequests
        //         ],
        //         RemovedPermissions:
        //         [
        //             PermissionNames.CanManageLeaveRequests
        //         ])
        // }
        ;

    public Task<IReadOnlyList<string>> GetPermissionsAsync(
        StaticApplicationRole role,
        string userName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var effectivePermissions = new HashSet<string>(
            PermissionsByRole.TryGetValue(role, out var rolePermissions)
                ? rolePermissions
                : [],
            StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(userName)
            && PermissionOverridesByUser.TryGetValue(userName.Trim(), out var permissionOverride))
        {
            foreach (var removedPermission in permissionOverride.RemovedPermissions)
            {
                effectivePermissions.Remove(removedPermission);
            }

            foreach (var addedPermission in permissionOverride.AddedPermissions)
            {
                effectivePermissions.Add(addedPermission);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>([.. effectivePermissions]);
    }
}
