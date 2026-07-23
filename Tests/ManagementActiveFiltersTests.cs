using Bunit;
using IK.Web.Components;

namespace IK.Web.Tests;

public sealed class ManagementActiveFiltersTests
{
    [Fact]
    public void Component_RendersAppliedFiltersAndRaisesRemovalCallbacks()
    {
        using var context = new BunitContext();
        string? removedKey = null;
        var clearAllRaised = false;

        var component = context.Render<ManagementActiveFilters>(parameters => parameters
            .Add(component => component.Filters,
            [
                new ManagementFilter("employee", "Çalışan: Ada Lovelace"),
                new ManagementFilter("status", "Durum: Aktif")
            ])
            .Add(component => component.OnRemove, key => removedKey = key)
            .Add(component => component.OnClearAll, () => clearAllRaised = true));

        Assert.Contains("Aktif filtreler", component.Markup);
        Assert.Contains("Çalışan: Ada Lovelace", component.Markup);
        Assert.Contains("Durum: Aktif", component.Markup);

        component.Find("button[aria-label='Çalışan: Ada Lovelace filtresini kaldır']").Click();
        Assert.Equal("employee", removedKey);

        component.Find(".management-filters-clear").Click();
        Assert.True(clearAllRaised);
    }

    [Fact]
    public async Task OptionSearch_ReturnsUniqueCaseInsensitiveMatchesInDisplayOrder()
    {
        var matches = await ManagementFilterOptionSearch.SearchAsync(
            ["Beta", "alpha", "ALPHA", "  Gamma  ", null, ""],
            "a",
            CancellationToken.None);

        Assert.Equal(["alpha", "Beta", "Gamma"], matches);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  İnsan Kaynakları  ", "İnsan Kaynakları")]
    public void FilterValueNormalization_TrimsOrRemovesEmptyText(string? value, string? expected)
    {
        Assert.Equal(expected, ManagementFilterValue.Normalize(value));
    }
}
