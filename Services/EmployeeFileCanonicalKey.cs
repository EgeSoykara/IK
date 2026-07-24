using System.Globalization;
using System.Text.RegularExpressions;

namespace IK.Web.Services;

public static partial class EmployeeFileCanonicalKey
{
    public static string CreateProfilePhoto(int employeeId, Guid fileId, string extension)
    {
        ValidateEmployeeId(employeeId);
        return $"employees/{employeeId.ToString("D10", CultureInfo.InvariantCulture)}/profile-photo/{fileId:N}{NormalizeExtension(extension)}";
    }

    public static string CreateDocument(
        int employeeId,
        string categoryCanonicalKey,
        Guid fileId,
        string extension)
    {
        ValidateEmployeeId(employeeId);
        ValidateCategory(categoryCanonicalKey);
        return $"employees/{employeeId.ToString("D10", CultureInfo.InvariantCulture)}/documents/{categoryCanonicalKey}/{fileId:N}{NormalizeExtension(extension)}";
    }

    public static void ValidateCategory(string categoryCanonicalKey)
    {
        if (string.IsNullOrWhiteSpace(categoryCanonicalKey)
            || categoryCanonicalKey.Length > 64
            || !CategoryCanonicalKeyPattern().IsMatch(categoryCanonicalKey))
        {
            throw new EmployeeFileValidationException(
                "Belge kategorisi geçerli bir canonical key olmalıdır.");
        }
    }

    private static void ValidateEmployeeId(int employeeId)
    {
        if (employeeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(employeeId));
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)
            || extension[0] != '.'
            || extension.Length > 10
            || extension.Count(character => character == '.') != 1
            || extension.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '.'))
        {
            throw new EmployeeFileValidationException("Dosya uzantısı geçersiz.");
        }

        return extension.ToLowerInvariant();
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryCanonicalKeyPattern();
}
