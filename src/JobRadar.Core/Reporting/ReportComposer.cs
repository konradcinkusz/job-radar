using System.Globalization;
using System.Text;

namespace JobRadar.Core.Reporting;

/// <summary>
/// Composes the daily Markdown report (SPEC §6.8). Pure: BCL only, no I/O, no clock, no environment.
/// Lines end with LF and every number is formatted with <see cref="CultureInfo.InvariantCulture"/>,
/// so identical input gives byte-identical output.
/// </summary>
public static class ReportComposer
{
    private const int ColumnCount = 9;
    private const string Separator = "|---|---|---|---|---|---|---|---|---|";

    /// <summary>Composes the report. The output ends with exactly one LF after the footer line.</summary>
    public static string Compose(ReportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Labels);
        if (input.Labels.Columns.Count != ColumnCount)
        {
            throw new ArgumentException($"Labels.Columns must hold {ColumnCount} entries.", nameof(input));
        }

        ReportLabels labels = input.Labels;
        var sb = new StringBuilder();

        // 1. Title and optional Reporter summary.
        sb.Append(FormattableString.Invariant($"# JobRadar {input.RunDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}\n\n"));
        if (!string.IsNullOrWhiteSpace(input.SummaryMd))
        {
            sb.Append(input.SummaryMd.Trim()).Append("\n\n");
        }

        // 2. Offer table, or the empty-day line when there are no rows.
        if (input.Rows.Count == 0)
        {
            sb.Append(labels.EmptyDay).Append("\n\n");
        }
        else
        {
            AppendTable(sb, input);
        }

        // 3. Offers left pending.
        sb.Append("## ").Append(labels.NotEvaluated).Append("\n\n");
        sb.Append(input.NotEvaluatedCount.ToString(CultureInfo.InvariantCulture)).Append("\n\n");

        // 4. Source health, then prefilter counts when there are any.
        sb.Append("## ").Append(labels.SourceHealth).Append("\n\n");
        foreach (SourceHealthLine source in input.Sources)
        {
            AppendSourceLine(sb, source, labels);
        }

        sb.Append('\n');
        if (input.PrefilterCounts.Count > 0)
        {
            string pairs = string.Join(", ", input.PrefilterCounts
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => FormattableString.Invariant($"{pair.Key}={pair.Value}")));
            sb.Append(labels.Prefilter).Append(": ").Append(pairs).Append("\n\n");
        }

        // 5. Cost per agent, or the unknown line when there are none.
        sb.Append("## ").Append(labels.Cost).Append("\n\n");
        if (input.Costs.Count == 0)
        {
            sb.Append(labels.Unknown).Append('\n');
        }
        else
        {
            foreach (CostLine cost in input.Costs)
            {
                AppendCostLine(sb, cost, labels);
            }
        }

        sb.Append('\n');

        // 6. Footer.
        string prompts = input.PromptVersions.Count == 0 ? "-" : string.Join(", ", input.PromptVersions);
        sb.Append("---\n\n");
        sb.Append(FormattableString.Invariant($"criteria v{input.CriteriaVersion} | prompts {prompts} | commit {input.Commit}")).Append('\n');

        return sb.ToString();
    }

    private static void AppendTable(StringBuilder sb, ReportInput input)
    {
        IReadOnlyList<string> columns = input.Labels.Columns;
        sb.Append("| ").Append(string.Join(" | ", columns)).Append(" |\n");
        sb.Append(Separator).Append('\n');

        IEnumerable<ReportRow> ordered = input.Rows
            .OrderByDescending(row => row.Score)
            .ThenBy(row => row.Date)
            .ThenBy(row => row.Code, StringComparer.Ordinal);

        foreach (ReportRow row in ordered)
        {
            string status = row.Status == ReportStatus.Alive ? input.Labels.StatusAlive : input.Labels.StatusUnverified;
            sb.Append("| ").Append(row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append(" | ").Append(EscapeCell(row.Company))
                .Append(" | ").Append(EscapeCell(row.Role))
                .Append(" | [").Append(columns[3]).Append("](").Append(EscapeUrl(row.Url)).Append(')')
                .Append(" | ").Append(EscapeCell(row.WorkMode))
                .Append(" | ").Append(EscapeCell(row.Track))
                .Append(" | ").Append(EscapeCell(row.Salary))
                .Append(" | ").Append(status)
                .Append(" | ").Append(row.Code)
                .Append(" |\n");
        }

        sb.Append('\n');
    }

    private static void AppendSourceLine(StringBuilder sb, SourceHealthLine source, ReportLabels labels)
    {
        if (source.NotRun)
        {
            sb.Append(FormattableString.Invariant($"- {source.Source}: {labels.NotRun}\n"));
            return;
        }

        string median = source.Median is { } value
            ? value.ToString("0.#", CultureInfo.InvariantCulture)
            : labels.Unknown;
        sb.Append(FormattableString.Invariant($"- {source.Source}: {source.Count} ({labels.Median} {median})"));
        if (source.Unhealthy)
        {
            sb.Append(FormattableString.Invariant($" - {labels.Alert}: {source.Reason ?? string.Empty}"));
        }

        sb.Append('\n');
    }

    private static void AppendCostLine(StringBuilder sb, CostLine cost, ReportLabels labels)
    {
        string credits = cost.AiCredits is { } value
            ? value.ToString("0.####", CultureInfo.InvariantCulture)
            : labels.Unknown;
        string usd = cost.Usd is { } dollars
            ? "$" + dollars.ToString("0.####", CultureInfo.InvariantCulture)
            : labels.Unknown;
        sb.Append(FormattableString.Invariant(
            $"- {cost.Agent}: {cost.Batches} {labels.Batches}, {cost.InputTokens}/{cost.OutputTokens} tokens, {credits} {labels.Credits}, {usd}\n"));
    }

    /// <summary>A pipe becomes <c>\|</c>; each CR or LF becomes one space so a row stays on one line.</summary>
    private static string EscapeCell(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '|':
                    sb.Append("\\|");
                    break;
                case '\r':
                case '\n':
                    sb.Append(' ');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>A pipe in a URL becomes <c>%7C</c>, so the link cannot break the table.</summary>
    private static string EscapeUrl(string value) => value.Replace("|", "%7C", StringComparison.Ordinal);
}
