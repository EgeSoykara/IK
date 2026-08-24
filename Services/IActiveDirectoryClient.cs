namespace IK.Web.Services;

public interface IActiveDirectoryClient
{
    ActiveDirectoryUser? Authenticate(string identifier, string password);
}

public sealed record ActiveDirectoryUser(
    string SamAccountName,
    string? GivenName,
    string? Surname,
    string? EmailAddress,
    string? VoiceTelephoneNumber);
