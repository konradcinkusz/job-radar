using JobRadar.Core.Models;
using YamlDotNet.RepresentationModel;

namespace JobRadar.Config;

/// <summary>Maps the <c>sources</c> mapping of <c>sources.yaml</c> to <see cref="SourceConfig"/> (SPEC Appendix B, §6.2).</summary>
internal static class SourcesParser
{
    private static readonly string[] EntryKeys =
        ["enabled", "kind", "stage", "expiry_by_absence", "companies", "attribution", "delay_hours", "countries", "repairable", "browser"];

    private static readonly string[] CompanyKeys = ["slug", "name"];

    public static IReadOnlyList<SourceConfig> Parse(ConfigReader r, YamlMappingNode root)
    {
        r.UnknownKeys(root, "", ["sources"]);
        var sources = new List<SourceConfig>();
        if (r.Node(root, "", "sources", required: true) is not { } node || r.Mapping(node, "sources") is not { } map)
        {
            return sources;
        }

        foreach (var (key, value) in map.Children)
        {
            var id = r.Key(key, "sources");
            if (id is null)
            {
                continue;
            }

            var path = ConfigReader.Join("sources", id);
            if (Rules.IsForbidden(id))
            {
                r.Error(path, Rules.ForbiddenMessage(id));
            }
            else if (!Rules.SourceId.IsMatch(id))
            {
                r.Error(path, $"must match {Rules.SourceId}");
            }
            else if (r.Mapping(value, path) is { } entry && Entry(r, id, path, entry) is { } source)
            {
                sources.Add(source);
            }
        }

        return sources;
    }

    private static SourceConfig? Entry(ConfigReader r, string id, string path, YamlMappingNode e)
    {
        r.UnknownKeys(e, path, EntryKeys);
        var enabled = r.Bool(e, path, "enabled", required: true);
        var kind = r.EnumValue<SourceKind>(e, path, "kind");
        var stage = r.Int(e, path, "stage", true, 0, 4, "must be an integer between 0 and 4");
        var expiry = r.Bool(e, path, "expiry_by_absence", required: true);
        var companies = Companies(r, e, path);
        var attribution = r.String(e, path, "attribution", required: false);
        r.Int(e, path, "delay_hours", false, int.MinValue, int.MaxValue, "must be an integer"); // validated, not used by Core
        var countries = r.Strings(e, path, "countries", required: false);
        var repairable = r.Bool(e, path, "repairable", required: false) ?? false;
        var browser = r.Bool(e, path, "browser", required: false) ?? false;

        if (enabled is null || kind is null || stage is null || expiry is null)
        {
            return null;
        }

        return new SourceConfig(id, enabled.Value, kind.Value, stage.Value, expiry.Value, companies,
            attribution, countries, repairable, browser);
    }

    private static List<CompanyRef> Companies(ConfigReader r, YamlMappingNode e, string path)
    {
        var companies = new List<CompanyRef>();
        var items = r.Sequence(e, path, "companies", required: false);
        var itemsPath = ConfigReader.Join(path, "companies");
        for (var i = 0; i < items.Count; i++)
        {
            var itemPath = $"{itemsPath}[{i}]";
            if (r.Mapping(items[i], itemPath) is not { } company)
            {
                continue;
            }

            r.UnknownKeys(company, itemPath, CompanyKeys);
            var slug = r.Matching(company, itemPath, "slug", required: true, Rules.Slug);
            var name = r.String(company, itemPath, "name", required: false);
            if (slug is not null)
            {
                companies.Add(new CompanyRef(slug, name));
            }
        }

        return companies;
    }
}
