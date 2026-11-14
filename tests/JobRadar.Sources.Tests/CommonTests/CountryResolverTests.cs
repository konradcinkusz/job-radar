using JobRadar.Sources.Common;

namespace JobRadar.Sources.Tests.CommonTests;

public sealed class CountryResolverTests
{
    [Fact]
    public void Country_name_resolves()
    {
        Assert.Equal("DE", CountryResolver.FromLocation("Berlin, Germany"));
        Assert.Equal("PL", CountryResolver.FromLocation("Remote (Poland)"));
    }

    [Fact]
    public void Local_country_name_resolves()
    {
        Assert.Equal("DE", CountryResolver.FromLocation("München, Deutschland"));
        Assert.Equal("ES", CountryResolver.FromLocation("Madrid, España"));
    }

    [Fact]
    public void Trailing_code_resolves()
    {
        Assert.Equal("PL", CountryResolver.FromLocation("Warsaw, PL"));
    }

    [Fact]
    public void Bare_city_is_null()
    {
        Assert.Null(CountryResolver.FromLocation("Dublin"));
        Assert.Null(CountryResolver.FromLocation("Warsaw"));
    }

    [Fact]
    public void Null_or_blank_is_null()
    {
        Assert.Null(CountryResolver.FromLocation(null));
        Assert.Null(CountryResolver.FromLocation(string.Empty));
        Assert.Null(CountryResolver.FromLocation("   "));
    }

    [Fact]
    public void Code_inside_a_longer_segment_is_not_a_country()
    {
        // "IT" is accepted only as the whole last segment, never inside "Remote - IT".
        Assert.Null(CountryResolver.FromLocation("Remote - IT"));
    }

    [Fact]
    public void Last_segment_wins()
    {
        // "Paris, France, Germany": the last segment is searched first, so Germany (DE) wins over France (FR).
        Assert.Equal("DE", CountryResolver.FromLocation("Paris, France, Germany"));
    }

    [Fact]
    public void Words_are_matched_whole()
    {
        Assert.Null(CountryResolver.FromLocation("Grenada"));
        Assert.Equal("RO", CountryResolver.FromLocation("Romania"));
        Assert.Null(CountryResolver.FromLocation("Romanian-speaking team"));
    }

    [Fact]
    public void Lower_case_code_is_not_a_trailing_code()
    {
        Assert.Null(CountryResolver.FromLocation("Warsaw, pl"));
    }

    [Fact]
    public void Trailing_code_must_be_the_last_segment()
    {
        // "PL" is not last here, so only names count; "Warsaw" has no name.
        Assert.Null(CountryResolver.FromLocation("PL, Warsaw"));
    }
}
