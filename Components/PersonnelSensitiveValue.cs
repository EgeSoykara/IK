namespace IK.Web.Components;

public static class PersonnelSensitiveValue
{
    public static string Mask(
        string? value,
        int visiblePrefix = 0,
        int visibleSuffix = 4)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var normalized = value.Trim();
        var prefixLength = Math.Max(0, visiblePrefix);
        var suffixLength = Math.Max(0, visibleSuffix);
        var visibleLength = prefixLength + suffixLength;
        if (normalized.Length <= visibleLength)
        {
            return new string('•', normalized.Length);
        }

        return string.Concat(
            normalized.AsSpan(0, prefixLength),
            new string('•', normalized.Length - visibleLength),
            normalized.AsSpan(normalized.Length - suffixLength));
    }
}
