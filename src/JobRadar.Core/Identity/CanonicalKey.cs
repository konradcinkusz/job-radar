namespace JobRadar.Core.Identity;

/// <summary>
/// Canonical key of an offer (SPEC §6.1): <c>Company(company)|Title(title)|COUNTRY</c>.
/// Offers with the same key form a duplicate group across sources (SPEC §6.4).
/// </summary>
public static class CanonicalKey
{
    /// <summary>Builds the key. A null country becomes <c>XX</c>.</summary>
    public static string Of(string company, string title, string? country) =>
        Normalizer.Company(company) + "|" + Normalizer.Title(title) + "|" + (country ?? "xx").ToUpperInvariant();
}
