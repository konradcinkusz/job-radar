using System.Text.RegularExpressions;
using JobRadar.Core.Identity;
using JobRadar.Core.Models;

namespace JobRadar.Core.Filtering;

/// <summary>Outcome of the Stage 1 prefilter (SPEC §6.6): <see cref="Pass"/>, or <see cref="Reject"/> with the reason id stored in <c>prefilter_reason</c>.</summary>
public abstract record PrefilterResult
{
    public sealed record Pass : PrefilterResult;

    public sealed record Reject(string ReasonId) : PrefilterResult;
}

/// <summary>
/// Stage 1 of SPEC §6.6, code only. The checks run in the order of the SPEC table; the first one that fires is returned.
/// Pure: BCL and <see cref="Normalizer"/> only, no I/O.
/// </summary>
public static class Prefilter
{
    /// <summary>".net" as a whole token: not followed by a letter or digit, so "internet" does not match.</summary>
    private static readonly Regex DotNetKeyword = new(@"\.net(?![\p{L}\p{N}])", RegexOptions.CultureInvariant);

    /// <summary>"c#" not preceded by a letter or digit, so the "#" is kept and the keyword is not normalised away.</summary>
    private static readonly Regex CSharpKeyword = new(@"(?<![\p{L}\p{N}])c#", RegexOptions.CultureInvariant);

    public static PrefilterResult Evaluate(RawOffer offer, Criteria criteria, IReadOnlySet<string> blocklist)
    {
        if (!TitleHasKeyword(offer.Title, criteria.TitleKeywords))
        {
            return new PrefilterResult.Reject("title-no-keyword");
        }

        if (offer.WorkMode == WorkMode.Onsite)
        {
            return new PrefilterResult.Reject("mode-onsite");
        }

        if (offer.WorkMode == WorkMode.Hybrid
            && offer.OfficeDaysPerWeek is int days
            && days > criteria.MaxOfficeDaysPerWeek)
        {
            return new PrefilterResult.Reject("mode-hybrid-heavy");
        }

        if (blocklist.Contains(Normalizer.Company(offer.Company)))
        {
            return new PrefilterResult.Reject("blocklist-company");
        }

        if (!CountryInScope(offer.Country, criteria.Countries))
        {
            return new PrefilterResult.Reject("country-outside-scope");
        }

        return new PrefilterResult.Pass();
    }

    /// <summary>
    /// True when any keyword matches. Keywords are case-insensitive. An empty list matches nothing, so every offer is rejected.
    /// ".net" and "c#" match the raw lowercased title, because normalisation would turn them into "net" and "c".
    /// Every other keyword is normalised and must appear as a whole token or whole token phrase of the normalised title.
    /// </summary>
    private static bool TitleHasKeyword(string title, IReadOnlyList<string> keywords)
    {
        string raw = title.ToLowerInvariant();
        string normalised = Normalizer.Title(title);
        string padded = " " + normalised + " ";

        foreach (string keyword in keywords)
        {
            string k = keyword.Trim().ToLowerInvariant();
            if (k == ".net")
            {
                if (DotNetKeyword.IsMatch(raw)) return true;
                continue;
            }

            if (k == "c#")
            {
                if (CSharpKeyword.IsMatch(raw)) return true;
                continue;
            }

            string nk = Normalizer.Title(k);
            if (nk.Length == 0)
            {
                continue;
            }

            if (padded.Contains(" " + nk + " ", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A missing or blank country passes. A known country must be in the criteria list, compared case-insensitively.</summary>
    private static bool CountryInScope(string? country, IReadOnlyList<string> countries)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return true;
        }

        string code = country.Trim().ToUpperInvariant();
        return countries.Any(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));
    }
}
