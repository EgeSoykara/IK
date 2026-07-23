using IK.Web.Database;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class StaticLoginService(HumanResourcesDbContext dbContext)
{
    private static readonly StaticLoginUser[] StaticUsers =
    [
        new("user", "user123", "Kullanıcı", 1, StaticApplicationRole.Employee),
        new("admin", "admin123", "Yönetici", 2, StaticApplicationRole.Administrator),
        new("hr", "hr123", "İnsan Kaynakları", 34, StaticApplicationRole.HumanResources)
    ];

    public async Task<StaticLoginAuthenticationResult> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserName = userName.Trim();
        var user = StaticUsers.FirstOrDefault(candidate =>
            string.Equals(candidate.UserName, normalizedUserName, StringComparison.OrdinalIgnoreCase) &&
            candidate.Password == password);

        if (user is null)
        {
            return new StaticLoginAuthenticationResult(false, null, null);
        }

        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(item => item.EmployeeId == user.EmployeeId, cancellationToken);

        return new StaticLoginAuthenticationResult(true, user, employee);
    }
}
