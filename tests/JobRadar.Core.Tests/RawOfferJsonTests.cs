using JobRadar.Core.Models;

namespace JobRadar.Core.Tests;

public class RawOfferJsonTests
{
    private static readonly RawOffer Full = new(
        Source: "greenhouse", SourceId: "123", Url: "https://example.org/jobs/123", Title: "Senior .NET Developer", Company: "Acme",
        Country: "PL", Location: "Warsaw", WorkMode: WorkMode.Hybrid, OfficeDaysPerWeek: 1, Contract: "b2b",
        SalaryMin: 20000, SalaryMax: 30000, Currency: "PLN", SalaryPeriod: SalaryPeriod.Month,
        Description: "Line one\nLine two", PostedAt: new DateTimeOffset(2026, 11, 12, 8, 30, 0, TimeSpan.Zero), Attribution: "Remotive");

    [Fact]
    public void Roundtrip_keeps_every_field()
    {
        var back = RawOfferJson.FromLine(RawOfferJson.ToLine(Full));
        Assert.Equal(Full, back);
    }

    [Fact]
    public void Roundtrip_keeps_nulls_as_nulls()
    {
        var sparse = Full with { Country = null, OfficeDaysPerWeek = null, SalaryMin = null, SalaryMax = null, Currency = null, SalaryPeriod = null, PostedAt = null, Attribution = null, Contract = null, Location = null };
        Assert.Equal(sparse, RawOfferJson.FromLine(RawOfferJson.ToLine(sparse)));
    }

    [Fact]
    public void Line_uses_snake_case_names_and_enum_names()
    {
        var line = RawOfferJson.ToLine(Full);
        Assert.Contains("\"source_id\":\"123\"", line);
        Assert.Contains("\"work_mode\":\"hybrid\"", line);
        Assert.Contains("\"salary_period\":\"month\"", line);
        Assert.DoesNotContain('\n', line);
    }

    [Fact]
    public void An_unknown_property_is_refused()
    {
        var line = RawOfferJson.ToLine(Full).Replace("\"title\"", "\"unexpected\":1,\"title\"", StringComparison.Ordinal);
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => RawOfferJson.FromLine(line));
    }

    [Fact]
    public void An_integer_for_an_enum_is_refused()
    {
        var line = RawOfferJson.ToLine(Full).Replace("\"hybrid\"", "1", StringComparison.Ordinal);
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => RawOfferJson.FromLine(line));
    }
}
