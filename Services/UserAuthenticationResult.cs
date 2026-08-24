using IK.Web.Models;

namespace IK.Web.Services;

public sealed record AuthenticatedUser(
    string Email,
    string DisplayName);

public sealed record UserAuthenticationResult(
    bool Succeeded,
    AuthenticatedUser? User,
    Employee? Employee)
{
    public static UserAuthenticationResult Failed { get; } = new(false, null, null);
}
