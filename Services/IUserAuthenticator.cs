namespace IK.Web.Services;

public interface IUserAuthenticator
{
    Task<UserAuthenticationResult> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default);
}
