using System.ComponentModel.DataAnnotations;
using IK.Web.Components;

namespace IK.Web.Services;

public static class PersonnelRecordValidator
{
    public static IEnumerable<ValidationResult> ValidateIdentityDocument(
        string? issuingAuthority,
        DateTime? issueDate,
        DateTime? expiryDate)
    {
        if (string.IsNullOrWhiteSpace(issuingAuthority))
        {
            yield return new ValidationResult(
                "Düzenleyen kurum zorunludur.",
                [nameof(issuingAuthority)]);
        }

        if (!issueDate.HasValue)
        {
            yield return new ValidationResult(
                "Düzenlenme tarihi zorunludur.",
                [nameof(issueDate)]);
        }

        if (issueDate.HasValue && expiryDate.HasValue && expiryDate.Value.Date < issueDate.Value.Date)
        {
            yield return new ValidationResult(
                "Son geçerlilik tarihi düzenlenme tarihinden önce olamaz.",
                [nameof(expiryDate)]);
        }
    }

    public static IEnumerable<ValidationResult> ValidateAddress(
        string? country,
        string? city,
        string? district)
    {
        if (string.IsNullOrWhiteSpace(district))
        {
            yield return new ValidationResult("İlçe/bölge zorunludur.", [nameof(district)]);
        }

        if (!string.IsNullOrWhiteSpace(country)
            && !PersonnelSelectOptions.CitiesFor(country).Contains(city, StringComparer.Ordinal))
        {
            yield return new ValidationResult("Seçilen şehir ülkeye ait değildir.", [nameof(city)]);
        }

        if (!string.IsNullOrWhiteSpace(country)
            && !string.IsNullOrWhiteSpace(city)
            && !PersonnelSelectOptions.DistrictsFor(country, city).Contains(district, StringComparer.Ordinal))
        {
            yield return new ValidationResult("Seçilen ilçe/bölge şehre ait değildir.", [nameof(district)]);
        }
    }

    public static IEnumerable<ValidationResult> ValidateEducation(
        string? programName,
        DateTime? startDate,
        bool isGraduated,
        string? degree,
        DateTime? graduationDate)
    {
        if (string.IsNullOrWhiteSpace(programName))
        {
            yield return new ValidationResult("Program/bölüm zorunludur.", [nameof(programName)]);
        }

        if (!startDate.HasValue)
        {
            yield return new ValidationResult("Başlangıç tarihi zorunludur.", [nameof(startDate)]);
        }

        if (isGraduated && string.IsNullOrWhiteSpace(degree))
        {
            yield return new ValidationResult("Mezun kaydı için diploma/derece zorunludur.", [nameof(degree)]);
        }

        if (isGraduated && !graduationDate.HasValue)
        {
            yield return new ValidationResult("Mezun kaydı için mezuniyet tarihi zorunludur.", [nameof(graduationDate)]);
        }

        if (startDate.HasValue
            && graduationDate.HasValue
            && graduationDate.Value.Date < startDate.Value.Date)
        {
            yield return new ValidationResult(
                "Mezuniyet tarihi başlangıç tarihinden önce olamaz.",
                [nameof(graduationDate)]);
        }
    }

    public static IEnumerable<ValidationResult> ValidateCourse(
        string? issuingOrganization,
        DateTime? startDate,
        DateTime? endDate,
        DateTime? expiryDate)
    {
        if (string.IsNullOrWhiteSpace(issuingOrganization))
        {
            yield return new ValidationResult("Düzenleyen kurum zorunludur.", [nameof(issuingOrganization)]);
        }

        if (!startDate.HasValue)
        {
            yield return new ValidationResult("Başlangıç tarihi zorunludur.", [nameof(startDate)]);
        }

        if (startDate.HasValue && endDate.HasValue && endDate.Value.Date < startDate.Value.Date)
        {
            yield return new ValidationResult(
                "Bitiş tarihi başlangıç tarihinden önce olamaz.",
                [nameof(endDate)]);
        }

        var expiryBaseline = endDate ?? startDate;
        if (expiryBaseline.HasValue
            && expiryDate.HasValue
            && expiryDate.Value.Date < expiryBaseline.Value.Date)
        {
            yield return new ValidationResult(
                "Geçerlilik tarihi eğitim tarihinden önce olamaz.",
                [nameof(expiryDate)]);
        }
    }
}
