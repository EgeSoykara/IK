namespace IK.Web.Services;

public interface IUserAuthenticator
{
    Task<UserAuthenticationResult> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);
}
