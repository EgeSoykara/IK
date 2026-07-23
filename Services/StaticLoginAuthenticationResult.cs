using IK.Web.Models;

namespace IK.Web.Services;

public sealed record StaticLoginAuthenticationResult(
    bool CredentialsMatched,
    StaticLoginUser? User,
    Employee? Employee)
{
    public bool Succeeded => CredentialsMatched && User is not null && Employee is not null;
}
