namespace IK.Web.Services;

public interface IUserAuthenticator
{
    Task<UserAuthenticationResult> AuthenticateAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken = default);
}
