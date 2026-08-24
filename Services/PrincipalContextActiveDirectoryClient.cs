using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using Microsoft.Extensions.Options;

namespace IK.Web.Services;

public sealed class PrincipalContextActiveDirectoryClient(
    IOptions<ActiveDirectoryOptions> options,
    ILogger<PrincipalContextActiveDirectoryClient> logger) : IActiveDirectoryClient
{
    public ActiveDirectoryUser? Authenticate(string identifier, string password)
    {
        if (!OperatingSystem.IsWindows())
        {
            logger.LogError("Active Directory authentication requires a Windows application host.");
            return null;
        }

        try
        {
            using var principalContext = new PrincipalContext(
                ContextType.Domain,
                options.Value.Domain.Trim());
            if (!principalContext.ValidateCredentials(
                    identifier,
                    password,
                    ContextOptions.Negotiate | ContextOptions.Signing))
            {
                return null;
            }

            using var user = UserPrincipal.FindByIdentity(
                principalContext,
                IdentityType.SamAccountName,
                identifier);
            if (user is null || user.IsAccountLockedOut() || user.Enabled == false)
            {
                return null;
            }

            return new ActiveDirectoryUser(
                user.SamAccountName ?? identifier,
                user.GivenName,
                user.Surname,
                user.EmailAddress,
                user.VoiceTelephoneNumber);
        }
        catch (PrincipalServerDownException exception)
        {
            logger.LogError(exception, "Active Directory domain controller is unavailable.");
            return null;
        }
        catch (DirectoryServicesCOMException exception)
        {
            logger.LogError(exception, "Active Directory authentication failed because of a directory service error.");
            return null;
        }
        catch (PrincipalOperationException exception)
        {
            logger.LogError(exception, "Active Directory authentication failed because of a principal operation error.");
            return null;
        }
    }
}
