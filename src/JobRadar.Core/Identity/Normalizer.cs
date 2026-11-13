using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JobRadar.Core.Identity;

/// <summary>
/// Company and title normalisation (SPEC §6.1). Pure: BCL only, no I/O.
/// Steps run in the order fixed by the director: NFKD, drop non-spacing marks, lowercase, remove markers
/// or legal-form words as whole tokens, replace every non-letter/digit with a space, collapse spaces.
/// </summary>
public static class Normalizer
{
    /// <summary>Legal-form words removed from company names. "spółka" is listed as written and in the form it takes after NFKD (ł is not decomposed).</summary>
    private static readonly string[] LegalForms =
    [
        "sp. z o.o.", "s.a.u.", "s.a.", "s.l.", "b.v.",
        "spółka", "społka", "spolka",
        "gmbh", "ltd", "llc", "inc", "sas", "srl",
        "ag", "oy", "ab", "as",
    ];

    /// <summary>Longest first, so "s.a.u." wins over "s.a.". Whole tokens only: bounded by start, end or non-letter-or-digit.</summary>
    private static readonly Regex LegalFormPattern = new(
        @"(?<![\p{L}\p{N}])(?:" + string.Join("|", LegalForms.OrderByDescending(w => w.Length).Select(Regex.Escape)) + @")(?![\p{L}\p{N}])",
        RegexOptions.CultureInvariant);

    /// <summary>Gender markers removed from titles. Parenthesised markers need no token boundary; bare "m/k" does.</summary>
    private static readonly Regex GenderMarkerPattern = new(
        @"\((?:m/f/d|m/w/d|f/m/x|all genders)\)|(?<![\p{L}\p{N}])m/k(?![\p{L}\p{N}])",
        RegexOptions.CultureInvariant);

    private static readonly Regex SpaceRun = new(" +", RegexOptions.CultureInvariant);

    /// <summary>Normalised company name: diacritics, case, legal-form words, punctuation and spacing removed.</summary>
    public static string Company(string s)
    {
        string lowered = Decompose(s);
        string withoutLegalForms = LegalFormPattern.Replace(lowered, " ");
        return Finish(withoutLegalForms);
    }

    /// <summary>Normalised job title: diacritics, case, gender markers, punctuation and spacing removed. Level words are kept.</summary>
    public static string Title(string s)
    {
        string lowered = Decompose(s);
        string withoutGenders = GenderMarkerPattern.Replace(lowered, " ");
        return Finish(withoutGenders);
    }

    /// <summary>Steps 1–3: NFKD, drop non-spacing marks (diacritics), lowercase invariant.</summary>
    private static string Decompose(string s)
    {
        string nfkd = s.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(nfkd.Length);
        foreach (Rune rune in nfkd.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(rune.ToString());
            }
        }

        return sb.ToString().ToLowerInvariant();
    }

    /// <summary>Steps 5–6: every character that is not a letter or digit becomes a space; runs of spaces collapse; trim.</summary>
    private static string Finish(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (Rune rune in s.EnumerateRunes())
        {
            sb.Append(Rune.IsLetterOrDigit(rune) ? rune.ToString() : " ");
        }

        return SpaceRun.Replace(sb.ToString(), " ").Trim();
    }
}
