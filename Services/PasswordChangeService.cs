using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IK.Web.Services;

public sealed class PasswordChangeService(
    HumanResourcesDbContext dbContext,
    EmployeeCredentialService credentialService,
    AuditLogService auditLogService)
{
    public async Task<PasswordChangeResult> ChangeRequiredPasswordAsync(
        ClaimsPrincipal principal,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var employeeId = principal.GetRequiredEmployeeId();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        var employee = await dbContext.Employees
            .AsNoTracking()
            .Include(item => item.Credential)
            .SingleOrDefaultAsync(
                item => item.EmployeeId == employeeId,
                cancellationToken);
        if (employee is null
            || employee.Credential is null
            || employee.Status != EmploymentStatus.Active
            || !employee.Credential.MustChangePassword)
        {
            throw new InvalidOperationException(
                "Şifre değiştirme isteği artık geçerli değil. Lütfen yeniden giriş yapın.");
        }

        credentialService.ChangePassword(employee, employee.Credential, newPassword);
        var changedRows = await dbContext.EmployeeCredentials
            .Where(credential =>
                credential.EmployeeId == employeeId
                && credential.MustChangePassword)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        credential => credential.PasswordHash,
                        employee.Credential.PasswordHash)
                    .SetProperty(
                        credential => credential.MustChangePassword,
                        false)
                    .SetProperty(
                        credential => credential.PasswordChangedAt,
                        employee.Credential.PasswordChangedAt),
                cancellationToken);
        if (changedRows != 1)
        {
            throw new InvalidOperationException(
                "Şifre değiştirme isteği artık geçerli değil. Lütfen yeniden giriş yapın.");
        }

        await auditLogService.AppendAsync(
            AuditActionType.PasswordChanged,
            nameof(EmployeeCredential),
            employee.EmployeeId.ToString(),
            employee.EmployeeId,
            "İlk girişte geçici şifre değiştirildi.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PasswordChangeResult(
            employee,
            new AuthenticatedUser(
                employee.Email,
                $"{employee.FirstName} {employee.LastName}",
                RequiresPasswordChange: false));
    }
}

public sealed record PasswordChangeResult(
    Employee Employee,
    AuthenticatedUser User);
