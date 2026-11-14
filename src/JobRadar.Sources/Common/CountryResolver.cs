using System.Text.RegularExpressions;

namespace JobRadar.Sources.Common;

/// <summary>
/// Resolves a job location to an ISO 3166-1 alpha-2 country code (SPEC Appendix A countries, plus US and CA).
/// A trailing code wins (<c>Warsaw, PL</c>); otherwise a country name, English or local, found as a whole word
/// (<c>Berlin, Germany</c>, <c>München, Deutschland</c>). A bare city resolves to null.
/// </summary>
public static class CountryResolver
{
    private static readonly (string Code, string[] Names)[] Countries =
    [
        ("AT", new[] { "Austria", "Österreich" }),
        ("BE", new[] { "Belgium", "België", "Belgique" }),
        ("BG", new[] { "Bulgaria" }),
        ("CH", new[] { "Switzerland", "Schweiz", "Suisse", "Svizzera" }),
        ("CY", new[] { "Cyprus" }),
        ("CZ", new[] { "Czechia", "Czech Republic", "Česko" }),
        ("DE", new[] { "Germany", "Deutschland" }),
        ("DK", new[] { "Denmark", "Danmark" }),
        ("EE", new[] { "Estonia" }),
        ("ES", new[] { "Spain", "España" }),
        ("FI", new[] { "Finland", "Suomi" }),
        ("FR", new[] { "France" }),
        ("GB", new[] { "United Kingdom", "Great Britain", "England", "Scotland", "Wales", "UK" }),
        ("GR", new[] { "Greece" }),
        ("HR", new[] { "Croatia", "Hrvatska" }),
        ("HU", new[] { "Hungary", "Magyarország" }),
        ("IE", new[] { "Ireland" }),
        ("IS", new[] { "Iceland" }),
        ("IT", new[] { "Italy", "Italia" }),
        ("LT", new[] { "Lithuania" }),
        ("LU", new[] { "Luxembourg" }),
        ("LV", new[] { "Latvia" }),
        ("MT", new[] { "Malta" }),
        ("NL", new[] { "Netherlands", "The Netherlands", "Holland", "Nederland" }),
        ("NO", new[] { "Norway", "Norge" }),
        ("PL", new[] { "Poland", "Polska" }),
        ("PT", new[] { "Portugal" }),
        ("RO", new[] { "Romania" }),
        ("SE", new[] { "Sweden", "Sverige" }),
        ("SI", new[] { "Slovenia" }),
        ("SK", new[] { "Slovakia", "Slovensko" }),
        ("UA", new[] { "Ukraine" }),
        ("RS", new[] { "Serbia", "Srbija" }),
        ("US", new[] { "United States", "United States of America", "USA" }),
        ("CA", new[] { "Canada" }),
    ];

    private static readonly HashSet<string> Codes = Countries.Select(c => c.Code).ToHashSet(StringComparer.Ordinal);

    // Lower-cased name -> code. A duplicate name throws at type initialisation, so an ambiguous table cannot ship.
    private static readonly Dictionary<string, string> CodeByName = Countries
        .SelectMany(c => c.Names.Select(n => (Name: n.ToLowerInvariant(), c.Code)))
        .ToDictionary(p => p.Name, p => p.Code, StringComparer.Ordinal);

    private static readonly Regex NamePattern = new(
        @"\b(?:" + string.Join("|", CodeByName.Keys.Select(Regex.Escape)) + @")\b",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns the upper-case alpha-2 code for <paramref name="location"/>, or null when neither a trailing code nor a country name
    /// is present. Rule 1: the last comma-separated segment is exactly two upper-case ASCII letters and a known code. Rule 2: a country
    /// name is searched in the last segment first, then in earlier segments right to left; the leftmost match in a segment wins.
    /// </summary>
    public static string? FromLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var segments = location.Split(',').Select(s => s.Trim()).ToArray();
        var last = segments[^1];
        if (IsCode(last))
        {
            return last;
        }

        for (var i = segments.Length - 1; i >= 0; i--)
        {
            var match = NamePattern.Match(segments[i].ToLowerInvariant());
            if (match.Success)
            {
                return CodeByName[match.Value];
            }
        }

        return null;
    }

    private static bool IsCode(string segment) =>
        segment.Length == 2
        && segment[0] is >= 'A' and <= 'Z'
        && segment[1] is >= 'A' and <= 'Z'
        && Codes.Contains(segment);
}
