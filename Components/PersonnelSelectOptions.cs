namespace IK.Web.Components;

public static class PersonnelSelectOptions
{
    public static IReadOnlyList<string> EducationLevels { get; } =
        ["Ön Lisans", "Lisans", "Yüksek Lisans", "Doktora"];

    public static bool IsEducationLevelAllowed(string? value)
    {
        return value is not null
               && EducationLevels.Contains(value, StringComparer.Ordinal);
    }

#if IK_E2E_PERSONNEL_OPTIONS
    // Compile-time-only browser fixture for the other categorical controls.
    public static IReadOnlyList<string> DocumentTypes { get; } = ["Kimlik Kartı", "Pasaport"];

    public static IReadOnlyList<string> PhoneTypes { get; } = ["Cep", "İş"];

    public static IReadOnlyList<string> AddressTypes { get; } = ["Ev", "İş"];

    public static IReadOnlyList<AddressCountryOption> AddressHierarchy { get; } =
    [
        new("KKTC",
        [
            new("Lefkoşa", ["Gönyeli", "Küçük Kaymaklı"]),
            new("Girne", ["Karakum", "Karaoğlanoğlu"])
        ]),
        new("Türkiye",
        [
            new("İstanbul", ["Kadıköy", "Beşiktaş"]),
            new("Ankara", ["Çankaya", "Keçiören"])
        ])
    ];

    public static IReadOnlyList<string> TerminationReasons { get; } = ["İstifa", "Emeklilik"];
#else
    public static IReadOnlyList<string> DocumentTypes { get; } = [];

    public static IReadOnlyList<string> PhoneTypes { get; } = [];

    public static IReadOnlyList<string> AddressTypes { get; } = [];

    public static IReadOnlyList<AddressCountryOption> AddressHierarchy { get; } = [];

    public static IReadOnlyList<string> TerminationReasons { get; } = [];
#endif

    public static IReadOnlyList<string> CitiesFor(string? country)
    {
        return AddressHierarchy
                   .FirstOrDefault(option =>
                       string.Equals(option.Name, country, StringComparison.Ordinal))
                   ?.Cities
                   .Select(option => option.Name)
                   .ToArray()
               ?? [];
    }

    public static IReadOnlyList<string> DistrictsFor(string? country, string? city)
    {
        return AddressHierarchy
                   .FirstOrDefault(option =>
                       string.Equals(option.Name, country, StringComparison.Ordinal))
                   ?.Cities
                   .FirstOrDefault(option =>
                       string.Equals(option.Name, city, StringComparison.Ordinal))
                   ?.Districts
               ?? [];
    }
}

public sealed record AddressCountryOption(
    string Name,
    IReadOnlyList<AddressCityOption> Cities);

public sealed record AddressCityOption(
    string Name,
    IReadOnlyList<string> Districts);
