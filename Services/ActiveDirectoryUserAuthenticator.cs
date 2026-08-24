using System.ComponentModel.DataAnnotations;
using System.Data;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class ActiveDirectoryUserAuthenticator(
    HumanResourcesDbContext dbContext,
    IActiveDirectoryClient activeDirectoryClient,
    AuditLogService auditLogService,
    ILogger<ActiveDirectoryUserAuthenticator> logger) : IUserAuthenticator
{
    private const int MaximumSamAccountNameLength = 256;
    private const int MaximumPasswordLength = 128;

    public async Task<UserAuthenticationResult> AuthenticateAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdentifier = EmployeeIdentityNormalizer.NormalizeSamAccountName(identifier);
        if (string.IsNullOrWhiteSpace(normalizedIdentifier)
            || string.IsNullOrEmpty(password)
            || normalizedIdentifier.Length > MaximumSamAccountNameLength
            || password.Length > MaximumPasswordLength
            || normalizedIdentifier.Contains('\\')
            || normalizedIdentifier.Contains('@'))
        {
            return UserAuthenticationResult.Failed;
        }

        var directoryUser = activeDirectoryClient.Authenticate(normalizedIdentifier, password);
        if (directoryUser is null)
        {
            return UserAuthenticationResult.Failed;
        }

        var samAccountName = EmployeeIdentityNormalizer.NormalizeSamAccountName(
            directoryUser.SamAccountName);
        var employee = await dbContext.Employees.SingleOrDefaultAsync(
            item => item.SamAccountName == samAccountName,
            cancellationToken);
        if (employee is null)
        {
            employee = await ProvisionEmployeeAsync(
                directoryUser,
                samAccountName,
                cancellationToken);
        }

        if (employee is null || employee.Status != EmploymentStatus.Active)
        {
            return UserAuthenticationResult.Failed;
        }

        var user = new AuthenticatedUser(
            employee.Email,
            $"{employee.FirstName} {employee.LastName}");
        return new UserAuthenticationResult(true, user, employee);
    }

    private async Task<Employee?> ProvisionEmployeeAsync(
        ActiveDirectoryUser directoryUser,
        string samAccountName,
        CancellationToken cancellationToken)
    {
        var firstName = directoryUser.GivenName?.Trim();
        var lastName = directoryUser.Surname?.Trim();
        var email = directoryUser.EmailAddress is null
            ? null
            : EmployeeIdentityNormalizer.NormalizeEmail(directoryUser.EmailAddress);
        if (string.IsNullOrWhiteSpace(firstName)
            || string.IsNullOrWhiteSpace(lastName)
            || string.IsNullOrWhiteSpace(email)
            || firstName.Length > 80
            || lastName.Length > 80
            || email.Length > 254
            || samAccountName.Length > 256
            || !new EmailAddressAttribute().IsValid(email))
        {
            logger.LogWarning(
                "Active Directory user {SamAccountName} cannot be provisioned because required profile attributes are missing or invalid.",
                samAccountName);
            return null;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var employee = await dbContext.Employees.SingleOrDefaultAsync(
            item => item.SamAccountName == samAccountName,
            cancellationToken);
        if (employee is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return employee;
        }

        employee = new Employee
        {
            SamAccountName = samAccountName,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            ApplicationRoleId = ApplicationRoleDefaults.EmployeeRoleId,
            Status = EmploymentStatus.Active
        };
        var phoneNumber = directoryUser.VoiceTelephoneNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(phoneNumber) && phoneNumber.Length <= 30)
        {
            employee.Phones.Add(new EmployeePhone
            {
                PhoneType = EmployeePhoneTypes.Work,
                PhoneNumber = phoneNumber,
                IsPrimary = true
            });
        }
        else if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            logger.LogWarning(
                "Active Directory phone number for {SamAccountName} exceeds the local maximum length and was not provisioned.",
                samAccountName);
        }

        dbContext.Employees.Add(employee);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.AppendSystemAsync(
                AuditActionType.EmployeeCreated,
                nameof(Employee),
                employee.EmployeeId.ToString(),
                SystemActorKeys.ActiveDirectoryProvisioning,
                $"SamAccountName={samAccountName}; ApplicationRoleId={ApplicationRoleDefaults.EmployeeRoleId}; ActiveDirectoryProvisioned=true",
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return employee;
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(
                exception,
                "Active Directory user {SamAccountName} could not be provisioned because a unique employee value already exists.",
                samAccountName);
            return null;
        }
    }
}
