using System.Globalization;
using IK.Web.Models;

namespace IK.Web.Services;

public static class AuditLogPresentation
{
    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = "Ad",
            ["DepartmentName"] = "Departman",
            ["DisplayName"] = "Görünen ad",
            ["EmployeeId"] = "Çalışan numarası",
            ["LeaveTypeId"] = "İzin türü numarası",
            ["RequestedDays"] = "Talep edilen gün",
            ["RefundDays"] = "İade edilen gün",
            ["EntitledDays"] = "Hak edilen gün",
            ["CarryOverDays"] = "Devreden gün",
            ["UsedDays"] = "Kullanılan gün",
            ["RemainingDays"] = "Kalan gün",
            ["Year"] = "Yıl",
            ["Date"] = "Tarih",
            ["Reason"] = "Neden",
            ["StartDate"] = "Başlangıç tarihi",
            ["EndDate"] = "Bitiş tarihi",
            ["ReturnDate"] = "İşe dönüş tarihi",
            ["OriginalEndDate"] = "İznin önceki bitiş tarihi",
            ["CancellationStartDate"] = "İptal başlangıç tarihi",
            ["CancellationEndDate"] = "İptal bitiş tarihi",
            ["Assigned"] = "İşlem yapılan çalışan",
            ["Skipped"] = "İşlem yapılmayan çalışan",
            ["Created"] = "Oluşturulan kayıt",
            ["Updated"] = "Güncellenen kayıt",
            ["Unchanged"] = "Değişmeyen kayıt",
            ["Ineligible"] = "Uygun olmayan çalışan",
            ["WarningChanges"] = "Güncellenen uyarı",
            ["Status"] = "Durum",
            ["Category"] = "İzin kategorisi",
            ["Scope"] = "Kapsam",
            ["Fields"] = "Değiştirilen alanlar",
            ["SizeBytes"] = "Dosya boyutu",
            ["ConfirmedOverLimit"] = "Sınır aşımı onayı",
            ["Retrospective"] = "Geriye dönük talep",
            ["CategoryCanonicalKey"] = "Belge kategorisi",
            ["ManagerChanged"] = "Departman yöneticisi değişti",
            ["ApplicationRoleId"] = "Uygulama rolü",
            ["Source"] = "Değişiklik kaynağı",
            ["HasManagementResponsibility"] = "Yönetim sorumluluğu var",
            ["Reviewed"] = "İncelendi",
            ["WithinLimit"] = "Uyarı sınırı içinde",
            ["Automatic"] = "Otomatik işlem",
            ["MissingStartDateEmployees"] = "İşe başlama tarihi eksik çalışan",
            ["PreviousManagerEmployeeId"] = "Önceki yönetici çalışan numarası",
            ["NewManagerEmployeeId"] = "Yeni yönetici çalışan numarası",
            ["TransferredLeaveRequests"] = "Yeni yöneticiye aktarılan izin talebi",
            ["BypassedLeaveRequests"] = "İK aşamasına geçirilen izin talebi",
            ["TransferredCancellationRequests"] = "Yeni yöneticiye aktarılan izin iptal talebi",
            ["BypassedCancellationRequests"] = "İK aşamasına geçirilen izin iptal talebi"
        };

    private static readonly IReadOnlyDictionary<string, string> FieldLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FirstName"] = "Ad",
            ["LastName"] = "Soyad",
            ["KktcKimlikNo"] = "KKTC kimlik numarası",
            ["Gender"] = "Cinsiyet",
            ["BloodGroup"] = "Kan grubu"
        };

    public static string FormatDetails(AuditLog log)
    {
        if (string.IsNullOrWhiteSpace(log.Details))
        {
            return "İşlem başarıyla tamamlandı.";
        }

        var details = log.Details.Trim();
        if (!details.Contains('='))
        {
            return details;
        }

        var parts = details
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(FormatPart)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        return parts.Length == 0 ? details : string.Join(" · ", parts);
    }

    private static string FormatPart(string part)
    {
        var separator = part.IndexOf('=');
        if (separator <= 0)
        {
            return part;
        }

        var key = part[..separator].Trim();
        var value = part[(separator + 1)..].Trim();
        var label = Labels.GetValueOrDefault(key, SplitIdentifier(key));
        return $"{label}: {FormatValue(key, value)}";
    }

    private static string FormatValue(string key, string value)
    {
        var changes = value.Split("->", StringSplitOptions.TrimEntries);
        if (changes.Length == 2)
        {
            return $"{FormatSingleValue(key, changes[0])} → {FormatSingleValue(key, changes[1])}";
        }

        return FormatSingleValue(key, value);
    }

    private static string FormatSingleValue(string key, string value)
    {
        if (key.Equals("ApplicationRoleId", StringComparison.OrdinalIgnoreCase))
        {
            return value switch
            {
                "1" => "Çalışan",
                "2" => "Yönetici",
                "3" => "İnsan Kaynakları",
                _ => $"Rol #{value}"
            };
        }

        if (key.Equals("Source", StringComparison.OrdinalIgnoreCase))
        {
            return value switch
            {
                "DepartmentManagerChanged" => "Departman yöneticisi değişikliği",
                "ManagerDelegationActivated" => "Vekâlet başlangıcı",
                "ManagerDelegationTransferred" => "Vekâlet devri",
                "ManagerDelegationRestored" => "Vekâlet sonu",
                "ManagerResponsibilityBackfill" => "Mevcut yönetim sorumluluğu geçişi",
                _ => value
            };
        }

        if (bool.TryParse(value, out var booleanValue))
        {
            return booleanValue ? "Evet" : "Hayır";
        }

        if (key.EndsWith("Days", StringComparison.OrdinalIgnoreCase)
            && decimal.TryParse(value, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var dayValue))
        {
            return $"{dayValue.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"))} gün";
        }

        if (key.EndsWith("Date", StringComparison.OrdinalIgnoreCase)
            && DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("tr-TR"));
        }

        if (key.Equals("SizeBytes", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sizeBytes))
        {
            return sizeBytes < 1024
                ? $"{sizeBytes} bayt"
                : $"{(sizeBytes / 1024m).ToString("0.#", CultureInfo.GetCultureInfo("tr-TR"))} KB";
        }

        if (key.Equals("Fields", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(", ", value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(field => FieldLabels.GetValueOrDefault(field, SplitIdentifier(field))));
        }

        return value switch
        {
            "ManagerReview" => "Yönetici onayı bekleniyor",
            "HumanResourcesReview" => "İK onayı bekleniyor",
            "Approved" => "Onaylandı",
            "Rejected" => "Reddedildi",
            "Cancelled" => "İptal edildi",
            "AnnualLeave" => "Yıllık izin",
            "SpecificLeaveType" => "Belirli izin türü",
            "Employee" => "Çalışan",
            "Department" => "Departman",
            "AllEmployees" => "Tüm çalışanlar",
            "Annual" => "Yıllık izin",
            "Male" => "Erkek",
            "Female" => "Kadın",
            "Active" => "Aktif",
            "Passive" => "Pasif",
            _ => value
        };
    }

    private static string SplitIdentifier(string value) =>
        string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));
}
