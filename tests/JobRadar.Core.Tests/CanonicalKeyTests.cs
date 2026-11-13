using JobRadar.Core.Identity;

namespace JobRadar.Core.Tests;

public class CanonicalKeyTests
{
    [Fact]
    public void CanonicalKey_uses_xx_when_country_is_null()
    {
        Assert.Equal("acme|senior net developer|XX", CanonicalKey.Of("Acme Sp. z o.o.", "Senior .NET Developer (m/f/d)", null));
    }

    [Theory]
    [InlineData("PL", "DE")]
    [InlineData("DE", "AT")]
    [InlineData("PL", null)]
    public void CanonicalKey_differs_across_countries(string? first, string? second)
    {
        Assert.NotEqual(
            CanonicalKey.Of("Acme", "Senior .NET Developer", first),
            CanonicalKey.Of("Acme", "Senior .NET Developer", second));
    }

    [Fact]
    public void CanonicalKey_is_built_from_company_title_and_upper_country()
    {
        Assert.Equal(
            Normalizer.Company("Zürich Tech GmbH") + "|" + Normalizer.Title("Senior .NET Developer (m/f/d)") + "|PL",
            CanonicalKey.Of("Zürich Tech GmbH", "Senior .NET Developer (m/f/d)", "pl"));
    }

    [Fact]
    public void CanonicalKey_matches_equivalent_offers_across_sources()
    {
        Assert.Equal(
            CanonicalKey.Of("Acme Sp. z o.o.", "Senior .NET Developer (m/f/d)", "PL"),
            CanonicalKey.Of("ACME", "senior .net developer", "pl"));
    }
}
