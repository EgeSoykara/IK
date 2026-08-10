using IK.Web.Models;

namespace IK.Web.Services;

public sealed record AuthenticatedUser(string UserName, string DisplayName);

public sealed record UserAuthenticationResult(
    bool CredentialsMatched,
    AuthenticatedUser? User,
    Employee? Employee)
{
    public bool Succeeded => CredentialsMatched && User is not null && Employee is not null;
}
