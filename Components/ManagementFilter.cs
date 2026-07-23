namespace IK.Web.Components;

public sealed record ManagementFilter(string Key, string Label);

public static class ManagementFilterValue
{
    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class ManagementFilterOptionSearch
{
    public static Task<IEnumerable<string>> SearchAsync(
        IEnumerable<string?> options,
        string value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedValue = value?.Trim();
        var matches = options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(option => string.IsNullOrWhiteSpace(normalizedValue)
                || option.Contains(normalizedValue, StringComparison.OrdinalIgnoreCase))
            .OrderBy(option => option, StringComparer.CurrentCultureIgnoreCase)
            .Take(20)
            .ToArray();

        return Task.FromResult<IEnumerable<string>>(matches);
    }
}
