using System.Text.RegularExpressions;
using JobRadar.Core.Models;
using YamlDotNet.RepresentationModel;

namespace JobRadar.Config;

/// <summary>Maps <c>criteria.yaml</c> to <see cref="Criteria"/> (SPEC Appendix A, §6.2).</summary>
internal static class CriteriaParser
{
    private static readonly string[] TopKeys =
    [
        "version", "countries", "max_office_days_per_week", "office_exception_countries", "office_penalty",
        "excluded_languages", "require_contract", "direct_employers_only", "threshold", "title_keywords",
        "tracks", "injection_patterns",
    ];

    private static readonly string[] TrackKeys = ["enabled", "label", "seniority", "role_family", "stack_main", "note"];

    public static Criteria? Parse(ConfigReader r, YamlMappingNode root)
    {
        r.UnknownKeys(root, "", TopKeys);
        var version = r.Int(root, "", "version", true, 1, int.MaxValue, "must be a positive integer");
        var countries = r.Strings(root, "", "countries", true, Rules.Country);
        var maxOfficeDays = r.Int(root, "", "max_office_days_per_week", true, 0, 5, "must be an integer between 0 and 5");
        var officeExceptions = r.Strings(root, "", "office_exception_countries", true, Rules.Country);
        var officePenalty = r.Int(root, "", "office_penalty", true, 0, 100, Rules.Percent);
        var excludedLanguages = r.Strings(root, "", "excluded_languages", true, Rules.Language);
        var requireContract = r.String(root, "", "require_contract", true);
        var directOnly = r.Bool(root, "", "direct_employers_only", true);
        var threshold = r.Int(root, "", "threshold", true, 0, 100, Rules.Percent);
        var titleKeywords = r.Strings(root, "", "title_keywords", true);
        var tracks = Tracks(r, root);
        var patterns = InjectionPatterns(r, root);

        if (version is null || maxOfficeDays is null || officePenalty is null || requireContract is null
            || directOnly is null || threshold is null)
        {
            return null;
        }

        return new Criteria(version.Value, countries, maxOfficeDays.Value, officeExceptions, officePenalty.Value,
            excludedLanguages, requireContract, directOnly.Value, threshold.Value, titleKeywords, tracks, patterns);
    }

    private static Dictionary<Track, TrackDefinition> Tracks(ConfigReader r, YamlMappingNode root)
    {
        var tracks = new Dictionary<Track, TrackDefinition>();
        if (r.Node(root, "", "tracks", required: true) is not { } node || r.Mapping(node, "tracks") is not { } map)
        {
            return tracks;
        }

        foreach (var (key, value) in map.Children)
        {
            var letter = r.Key(key, "tracks");
            if (letter is null)
            {
                continue;
            }

            var path = ConfigReader.Join("tracks", letter);
            if (letter is not ("A" or "B" or "C"))
            {
                r.Error(path, "track must be A, B or C");
                continue;
            }

            var track = letter switch { "A" => Track.A, "B" => Track.B, _ => Track.C };
            if (r.Mapping(value, path) is { } definition && TrackDef(r, path, definition) is { } parsed)
            {
                tracks[track] = parsed;
            }
        }

        return tracks;
    }

    private static TrackDefinition? TrackDef(ConfigReader r, string path, YamlMappingNode t)
    {
        r.UnknownKeys(t, path, TrackKeys);
        var enabled = r.Bool(t, path, "enabled", required: true);
        var label = r.String(t, path, "label", required: true);
        var seniority = r.EnumList<Seniority>(t, path, "seniority");
        var roleFamily = r.EnumList<RoleFamily>(t, path, "role_family");
        var stackMain = r.EnumList<StackMain>(t, path, "stack_main");
        var note = r.String(t, path, "note", required: false);

        if (enabled is null || label is null)
        {
            return null;
        }

        return new TrackDefinition(enabled.Value, label, seniority, roleFamily, stackMain, note);
    }

    private static List<string> InjectionPatterns(ConfigReader r, YamlMappingNode root)
    {
        var patterns = new List<string>();
        var items = r.Sequence(root, "", "injection_patterns", required: true);
        for (var i = 0; i < items.Count; i++)
        {
            var path = $"injection_patterns[{i}]";
            if (items[i] is not YamlScalarNode { Value: not null } scalar)
            {
                r.Error(path, "must be a string");
            }
            else if (!IsRegex(scalar.Value))
            {
                r.Error(path, "must be a valid regular expression");
            }
            else
            {
                patterns.Add(scalar.Value);
            }
        }

        return patterns;
    }

    private static bool IsRegex(string pattern)
    {
        try
        {
            _ = new Regex(pattern);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
