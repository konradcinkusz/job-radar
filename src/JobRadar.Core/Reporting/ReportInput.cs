namespace JobRadar.Core.Reporting;

/// <summary>Liveness of an offer as shown in the report. <c>Unverified</c> is labelled from <see cref="ReportLabels"/>.</summary>
public enum ReportStatus { Alive, Unverified }

/// <summary>One offer row of the report table. <c>Score</c> orders the rows and is not shown.</summary>
public sealed record ReportRow(DateOnly Date, string Company, string Role, string Url, string WorkMode, string Track, string Salary, ReportStatus Status, string Code, int Score);

/// <summary>Health of one source in the run. <c>Median</c> is null when unknown; <c>NotRun</c> lines say so.</summary>
public sealed record SourceHealthLine(string Source, int Count, double? Median, bool Unhealthy, string? Reason, bool NotRun);

/// <summary>Batches, tokens, AI credits and dollars of one agent. Null credits or dollars are shown as unknown.</summary>
public sealed record CostLine(string Agent, int Batches, int InputTokens, int OutputTokens, double? AiCredits, double? Usd);

/// <summary>Labels of the report, from <c>report.yaml</c>. <c>Columns</c> holds the nine table headers in order.</summary>
public sealed record ReportLabels(IReadOnlyList<string> Columns, string StatusAlive, string StatusUnverified, string EmptyDay, string NotEvaluated, string SourceHealth, string Median, string Alert, string NotRun, string Prefilter, string Cost, string Unknown, string Batches, string Credits);

/// <summary>Everything <see cref="ReportComposer"/> needs for one report. The composer reads nothing else.</summary>
public sealed record ReportInput(DateOnly RunDate, string? SummaryMd, IReadOnlyList<ReportRow> Rows, int NotEvaluatedCount, IReadOnlyList<SourceHealthLine> Sources, IReadOnlyDictionary<string, int> PrefilterCounts, IReadOnlyList<CostLine> Costs, int CriteriaVersion, IReadOnlyList<string> PromptVersions, string Commit, ReportLabels Labels);
