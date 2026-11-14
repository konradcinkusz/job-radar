using YamlDotNet.RepresentationModel;

namespace JobRadar.Config;

/// <summary>The settings of <c>report.yaml</c>: the score threshold, whether a score is required and the Polish report labels (SPEC §6.7, §6.8).</summary>
public sealed record ReportConfig(int Threshold, bool RequireScore, IReadOnlyDictionary<string, string> Labels);

/// <summary>Loads <c>report.yaml</c> (SPEC §6.7, §6.8).</summary>
public static partial class ConfigLoader
{
    private static readonly string[] ReportKeys = ["threshold", "require_score", "labels"];

    /// <summary>The label keys of SPEC §6.8, in file order. <c>labels</c> must hold exactly these keys.</summary>
    private static readonly string[] LabelKeys =
    [
        "column_date", "column_company", "column_role", "column_link", "column_work_mode", "column_track",
        "column_salary", "column_status", "column_code", "status_alive", "status_unverified", "empty_day",
        "not_evaluated", "source_health", "median", "alert", "not_run", "prefilter", "cost", "unknown",
        "batches", "credits",
    ];

    public static ConfigResult<ReportConfig> LoadReport(string path) =>
        Load(path, static (reader, root) => ParseReport(reader, root));

    private static ReportConfig? ParseReport(ConfigReader r, YamlMappingNode root)
    {
        r.UnknownKeys(root, "", ReportKeys);
        var threshold = r.Int(root, "", "threshold", true, 0, 100, Rules.Percent);
        var requireScore = r.Bool(root, "", "require_score", true);
        var labels = Labels(r, root);

        if (threshold is null || requireScore is null || labels is null)
        {
            return null;
        }

        return new ReportConfig(threshold.Value, requireScore.Value, labels);
    }

    private static Dictionary<string, string>? Labels(ConfigReader r, YamlMappingNode root)
    {
        if (r.Node(root, "", "labels", required: true) is not { } node || r.Mapping(node, "labels") is not { } map)
        {
            return null;
        }

        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in LabelKeys)
        {
            var path = $"labels.{key}";
            if (!map.Children.TryGetValue(new YamlScalarNode(key), out var value))
            {
                r.Error(path, "missing label");
            }
            else if (value is YamlScalarNode { Value: { Length: > 0 } text })
            {
                labels[key] = text;
            }
            else
            {
                r.Error(path, "must be a non-empty string");
            }
        }

        r.UnknownKeys(map, "labels", LabelKeys);
        return labels;
    }
}
