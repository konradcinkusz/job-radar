using JobRadar.Core.Filtering;
using JobRadar.Core.Identity;
using JobRadar.Core.Models;

namespace JobRadar.Core.Tests;

public class PrefilterTests
{
    private static readonly IReadOnlySet<string> NoBlocklist = new HashSet<string>();

    private static readonly IReadOnlySet<string> OneBlocked = new HashSet<string> { "body shop" };

    /// <summary>The criteria of SPEC Appendix A, with the values used in the tests.</summary>
    private static Criteria AppendixA() => new(
        Version: 1,
        Countries: ["AT", "BE", "BG", "CH", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GB", "GR", "HR", "HU", "IE", "IS", "IT", "LT", "LU", "LV", "MT", "NL", "NO", "PL", "PT", "RO", "SE", "SI", "SK", "UA", "RS"],
        MaxOfficeDaysPerWeek: 1,
        OfficeExceptionCountries: ["ES"],
        OfficePenalty: 15,
        ExcludedLanguages: ["de"],
        RequireContract: "b2b",
        DirectEmployersOnly: true,
        Threshold: 70,
        TitleKeywords: ["ai", "ml", "llm", ".net", "dotnet", "c#", "backend", "back-end", "machine learning", "generative"],
        Tracks: new Dictionary<Track, TrackDefinition>
        {
            [Track.A] = new(true, "Mid AI Engineer", [Seniority.Mid], [RoleFamily.AiEngineer], [], null),
            [Track.B] = new(true, "Senior, .NET main stack", [Seniority.Senior], [], [StackMain.Dotnet], null),
            [Track.C] = new(false, "undefined", [], [], [], "owner defines (DECISIONS D-27)"),
        },
        InjectionPatterns: []);

    /// <summary>An offer that passes every check of <see cref="AppendixA"/> with <see cref="NoBlocklist"/>. Tests change one field with <c>with</c>.</summary>
    private static RawOffer PassingOffer() => new(
        Source: "synthetic",
        SourceId: "synthetic-1",
        Url: "https://example.invalid/jobs/1",
        Title: "Mid AI Engineer",
        Company: "Acme Labs",
        Country: "DE",
        Location: null,
        WorkMode: WorkMode.Remote,
        OfficeDaysPerWeek: null,
        Contract: null,
        SalaryMin: null,
        SalaryMax: null,
        Currency: null,
        SalaryPeriod: null,
        Description: "Synthetic description.",
        PostedAt: null,
        Attribution: null);

    private static PrefilterResult Evaluate(RawOffer offer, Criteria? criteria = null, IReadOnlySet<string>? blocklist = null)
        => Prefilter.Evaluate(offer, criteria ?? AppendixA(), blocklist ?? NoBlocklist);

    private static void AssertReject(PrefilterResult result, string reasonId)
    {
        var reject = Assert.IsType<PrefilterResult.Reject>(result);
        Assert.Equal(reasonId, reject.ReasonId);
    }

    private static void AssertPass(PrefilterResult result)
    {
        Assert.IsType<PrefilterResult.Pass>(result);
    }

    [Fact]
    public void Reject_title_no_keyword()
    {
        AssertReject(Evaluate(PassingOffer() with { Title = "Office Manager" }), "title-no-keyword");
    }

    [Fact]
    public void Pass_when_title_has_keyword()
    {
        AssertPass(Evaluate(PassingOffer() with { Title = "Backend Engineer" }));
    }

    [Fact]
    public void Reject_mode_onsite()
    {
        AssertReject(Evaluate(PassingOffer() with { WorkMode = WorkMode.Onsite }), "mode-onsite");
    }

    [Fact]
    public void Pass_when_mode_is_remote()
    {
        AssertPass(Evaluate(PassingOffer() with { WorkMode = WorkMode.Remote }));
    }

    [Fact]
    public void Reject_mode_hybrid_heavy()
    {
        AssertReject(
            Evaluate(PassingOffer() with { WorkMode = WorkMode.Hybrid, OfficeDaysPerWeek = 2 }),
            "mode-hybrid-heavy");
    }

    [Fact]
    public void Pass_when_hybrid_days_within_limit()
    {
        AssertPass(Evaluate(PassingOffer() with { WorkMode = WorkMode.Hybrid, OfficeDaysPerWeek = 1 }));
    }

    [Fact]
    public void Reject_blocklist_company()
    {
        AssertReject(
            Evaluate(PassingOffer() with { Company = "Body Shop Sp. z o.o." }, blocklist: OneBlocked),
            "blocklist-company");
    }

    [Fact]
    public void Pass_when_company_not_blocked()
    {
        AssertPass(Evaluate(PassingOffer() with { Company = "Acme Labs" }, blocklist: OneBlocked));
    }

    [Fact]
    public void Reject_country_outside_scope()
    {
        AssertReject(Evaluate(PassingOffer() with { Country = "US" }), "country-outside-scope");
    }

    [Fact]
    public void Pass_when_country_in_scope_in_any_case()
    {
        AssertPass(Evaluate(PassingOffer() with { Country = "pl" }));
    }

    [Fact]
    public void Title_keyword_ai_does_not_match_inside_words()
    {
        // "maintainer" contains "ai" as letters only, not as a token.
        AssertReject(Evaluate(PassingOffer() with { Title = "Maintainer" }), "title-no-keyword");

        AssertPass(Evaluate(PassingOffer() with { Title = "AI Engineer" }));
    }

    [Fact]
    public void Hybrid_without_days_passes()
    {
        AssertPass(Evaluate(PassingOffer() with { WorkMode = WorkMode.Hybrid, OfficeDaysPerWeek = null }));
    }

    [Fact]
    public void Unknown_country_passes()
    {
        AssertPass(Evaluate(PassingOffer() with { Country = null }));
        AssertPass(Evaluate(PassingOffer() with { Country = "   " }));
    }

    [Fact]
    public void Reasons_are_checked_in_the_documented_order()
    {
        // Fails all five checks; each step fixes the earliest failing check and must expose the next one.
        RawOffer offer = PassingOffer() with
        {
            Title = "Office Manager",
            WorkMode = WorkMode.Onsite,
            Company = "Body Shop",
            Country = "US",
        };

        AssertReject(Evaluate(offer, blocklist: OneBlocked), "title-no-keyword");

        offer = offer with { Title = "AI Engineer" };
        AssertReject(Evaluate(offer, blocklist: OneBlocked), "mode-onsite");

        offer = offer with { WorkMode = WorkMode.Hybrid, OfficeDaysPerWeek = 2 };
        AssertReject(Evaluate(offer, blocklist: OneBlocked), "mode-hybrid-heavy");

        offer = offer with { WorkMode = WorkMode.Remote };
        AssertReject(Evaluate(offer, blocklist: OneBlocked), "blocklist-company");

        offer = offer with { Company = "Acme Labs" };
        AssertReject(Evaluate(offer, blocklist: OneBlocked), "country-outside-scope");

        offer = offer with { Country = "DE" };
        AssertPass(Evaluate(offer, blocklist: OneBlocked));
    }

    [Fact]
    public void Empty_keyword_list_rejects_everything()
    {
        Criteria noKeywords = AppendixA() with { TitleKeywords = [] };

        AssertReject(Evaluate(PassingOffer(), criteria: noKeywords), "title-no-keyword");
        AssertReject(Evaluate(PassingOffer() with { Title = "Backend Engineer" }, criteria: noKeywords), "title-no-keyword");
    }

    [Fact]
    public void Dotnet_keyword_matches_ASP_NET_Developer()
    {
        Criteria dotNetOnly = AppendixA() with { TitleKeywords = [".net"] };

        AssertPass(Evaluate(PassingOffer() with { Title = "ASP.NET Developer" }, criteria: dotNetOnly));
        AssertPass(Evaluate(PassingOffer() with { Title = "Senior .NET Developer" }, criteria: dotNetOnly));
    }

    [Fact]
    public void Csharp_keyword_matches_CSharp_dotnet_Engineer()
    {
        Criteria csharpOnly = AppendixA() with { TitleKeywords = ["c#"] };

        AssertPass(Evaluate(PassingOffer() with { Title = "C#/.NET Engineer" }, criteria: csharpOnly));
    }

    [Fact]
    public void Dotnet_keyword_does_not_match_internet()
    {
        Criteria dotNetOnly = AppendixA() with { TitleKeywords = [".net"] };

        AssertReject(Evaluate(PassingOffer() with { Title = "Internet Engineer" }, criteria: dotNetOnly), "title-no-keyword");
    }
}
