using IK.Web.Database;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class StaticUserAuthenticator(HumanResourcesDbContext dbContext) : IUserAuthenticator
{
    private static readonly StaticCredential[] Credentials =
    [
        new("user", "user123", "Kullanıcı", 2),
        new("admin", "admin123", "Yönetici", 1),
        new("hr", "hr123", "İnsan Kaynakları", 4)
    ];

    public async Task<UserAuthenticationResult> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserName = userName.Trim();
        var credential = Credentials.FirstOrDefault(candidate =>
            string.Equals(
                candidate.UserName,
                normalizedUserName,
                StringComparison.OrdinalIgnoreCase)
            && candidate.Password == password);

        if (credential is null)
        {
            return new UserAuthenticationResult(false, null, null);
        }

        var employee = await dbContext.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.EmployeeId == credential.EmployeeId,
                cancellationToken);
        var user = new AuthenticatedUser(credential.UserName, credential.DisplayName);

        return new UserAuthenticationResult(true, user, employee);
    }

    private sealed record StaticCredential(
        string UserName,
        string Password,
        string DisplayName,
        int EmployeeId);
}
