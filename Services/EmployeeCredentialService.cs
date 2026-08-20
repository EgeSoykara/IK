using IK.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace IK.Web.Services;

public sealed class EmployeeCredentialService(
    IPasswordHasher<EmployeeCredential> passwordHasher)
{
    public const int MinimumPasswordLength = 12;
    public const int MaximumPasswordLength = 128;

    public EmployeeCredential CreateInitial(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);
        if (string.IsNullOrWhiteSpace(employee.KktcKimlikNo))
        {
            throw new InvalidOperationException(
                "Geçici şifre oluşturmak için KKTC kimlik numarası zorunludur.");
        }

        var credential = new EmployeeCredential
        {
            Employee = employee,
            MustChangePassword = true
        };
        credential.PasswordHash = passwordHasher.HashPassword(
            credential,
            employee.KktcKimlikNo);
        return credential;
    }

    public PasswordVerificationResult Verify(
        EmployeeCredential credential,
        string password) =>
        passwordHasher.VerifyHashedPassword(
            credential,
            credential.PasswordHash,
            password);

    public void Rehash(EmployeeCredential credential, string password)
    {
        credential.PasswordHash = passwordHasher.HashPassword(credential, password);
    }

    public void ChangePassword(
        Employee employee,
        EmployeeCredential credential,
        string newPassword)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ArgumentNullException.ThrowIfNull(credential);

        if (string.Equals(newPassword, employee.KktcKimlikNo, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Yeni şifre KKTC kimlik numarasıyla aynı olamaz.");
        }

        if (string.IsNullOrWhiteSpace(newPassword)
            || newPassword.Length < MinimumPasswordLength
            || newPassword.Length > MaximumPasswordLength)
        {
            throw new InvalidOperationException(
                $"Yeni şifre {MinimumPasswordLength}-{MaximumPasswordLength} karakter arasında olmalıdır.");
        }

        credential.PasswordHash = passwordHasher.HashPassword(credential, newPassword);
        credential.MustChangePassword = false;
        credential.PasswordChangedAt = DateTimeOffset.UtcNow;
    }
}
