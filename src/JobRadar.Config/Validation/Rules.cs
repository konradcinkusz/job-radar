using System.Text.RegularExpressions;

namespace JobRadar.Config;

/// <summary>Fixed validation rules of SPEC §6.2.</summary>
internal static class Rules
{
    public static readonly Regex SourceId = new("^[a-z][a-z0-9-]{1,30}$", RegexOptions.CultureInvariant);
    public static readonly Regex Slug = new("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant);
    public static readonly Regex Country = new("^[A-Z]{2}$", RegexOptions.CultureInvariant);
    public static readonly Regex Language = new("^[a-z]{2}$", RegexOptions.CultureInvariant);

    public const string Percent = "must be an integer between 0 and 100";

    private static readonly string[] ForbiddenIds = ["linkedin", "indeed"];

    public static bool IsForbidden(string id) => ForbiddenIds.Contains(id);

    public static string ForbiddenMessage(string id) =>
        $"source id '{id}' is forbidden by DECISIONS D-03 (no scraping of LinkedIn or Indeed)";
}
