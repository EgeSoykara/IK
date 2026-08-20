using IK.Web.Database;
using IK.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class EmployeeUserAuthenticator : IUserAuthenticator
{
    private readonly HumanResourcesDbContext dbContext;
    private readonly EmployeeCredentialService credentialService;
    private readonly EmployeeCredential dummyCredential;

    public EmployeeUserAuthenticator(
        HumanResourcesDbContext dbContext,
        EmployeeCredentialService credentialService,
        IPasswordHasher<EmployeeCredential> passwordHasher)
    {
        this.dbContext = dbContext;
        this.credentialService = credentialService;
        dummyCredential = new EmployeeCredential();
        dummyCredential.PasswordHash = passwordHasher.HashPassword(
            dummyCredential,
            "authentication-timing-placeholder");
    }

    public async Task<UserAuthenticationResult> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var employee = await dbContext.Employees
            .Include(item => item.Credential)
            .SingleOrDefaultAsync(
                item => item.Email == normalizedEmail,
                cancellationToken);
        var credential = employee?.Credential ?? dummyCredential;
        var passwordHasValidLength = password.Length <= EmployeeCredentialService.MaximumPasswordLength;
        var verification = credentialService.Verify(
            credential,
            passwordHasValidLength ? password : "oversized-password-placeholder");

        if (employee is null
            || employee.Credential is null
            || employee.Status != EmploymentStatus.Active
            || !passwordHasValidLength
            || verification == PasswordVerificationResult.Failed)
        {
            return UserAuthenticationResult.Failed;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            credentialService.Rehash(employee.Credential, password);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var user = new AuthenticatedUser(
            employee.Email,
            $"{employee.FirstName} {employee.LastName}",
            employee.Credential.MustChangePassword);
        return new UserAuthenticationResult(true, user, employee);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
