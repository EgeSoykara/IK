using IK.Web.Components;

namespace IK.Web.Tests;

public sealed class PersonnelSensitiveValueTests
{
    [Theory]
    [InlineData("1234567890", 0, 4, "••••••7890")]
    [InlineData("TR123456789012345678901234", 2, 4, "TR••••••••••••••••••••1234")]
    [InlineData("1234", 0, 4, "••••")]
    [InlineData(null, 0, 4, "-")]
    public void Mask_RevealsOnlyExplicitPrefixAndSuffix(
        string? value,
        int visiblePrefix,
        int visibleSuffix,
        string expected)
    {
        Assert.Equal(
            expected,
            PersonnelSensitiveValue.Mask(value, visiblePrefix, visibleSuffix));
    }
}
