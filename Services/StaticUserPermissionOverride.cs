namespace IK.Web.Services;

public sealed record StaticUserPermissionOverride(
    IReadOnlyList<string> AddedPermissions,
    IReadOnlyList<string> RemovedPermissions);
