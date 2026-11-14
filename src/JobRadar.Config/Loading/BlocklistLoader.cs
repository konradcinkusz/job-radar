using JobRadar.Core.Identity;
using YamlDotNet.RepresentationModel;

namespace JobRadar.Config;

/// <summary>Loads <c>blocklist.yaml</c>: the companies the prefilter rejects (SPEC §6.6).</summary>
public static partial class ConfigLoader
{
    public static ConfigResult<IReadOnlySet<string>> LoadBlocklist(string path) =>
        Load<IReadOnlySet<string>>(path, static (reader, root) => ParseBlocklist(reader, root));

    private static HashSet<string>? ParseBlocklist(ConfigReader r, YamlMappingNode root)
    {
        r.UnknownKeys(root, "", ["companies"]);
        var companies = new HashSet<string>(StringComparer.Ordinal);
        var items = r.Sequence(root, "", "companies", required: true);
        for (var i = 0; i < items.Count; i++)
        {
            var itemPath = $"companies[{i}]";
            if (items[i] is not YamlScalarNode { Value: not null } scalar)
            {
                r.Error(itemPath, "must be a string");
                continue;
            }

            var normalised = Normalizer.Company(scalar.Value);
            if (normalised.Length == 0)
            {
                r.Error(itemPath, "must not be empty after normalisation");
            }
            else
            {
                companies.Add(normalised);
            }
        }

        return companies;
    }
}
