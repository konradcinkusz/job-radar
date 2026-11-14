using JobRadar.Core.Reporting;

namespace JobRadar.Core.Tests;

public class ReportComposerTests
{
    private const string EmptyDayLine = "Brak nowych ofert.";
    private const string HeaderLine = "| data | firma | rola | link | tryb pracy | ścieżka | widełki | status | kod |";
    private static readonly string[] ExpectedOrder = { "e5f6a7b8", "a1b2c3d4", "c9d0e1f2" };
    private static readonly string[] PromptVersionsSample = { "a1b2c3d4e5f6", "0f9e8d7c6b5a" };
    private static readonly string[] ColumnNames = { "data", "firma", "rola", "link", "tryb pracy", "ścieżka", "widełki", "status", "kod" };

    [Fact]
    public void Golden_report_matches_byte_for_byte()
    {
        string actual = ReportComposer.Compose(SampleInput());

        Assert.Equal(ReadGolden(), actual);
    }

    [Fact]
    public void Rows_are_sorted_by_score_then_first_seen()
    {
        // Scrambled on purpose: Acme (score 91, later date) is listed before Beta (score 91, earlier date).
        ReportRow[] rows = SampleRows();
        ReportRow acme = rows[0];
        ReportRow beta = rows[1];
        ReportRow gamma = rows[2];
        ReportInput input = SampleInput() with { Rows = new[] { acme, gamma, beta } };

        string output = ReportComposer.Compose(input);

        Assert.Equal(ExpectedOrder, TableCodes(output));
    }

    [Fact]
    public void Unverified_is_labelled()
    {
        string output = ReportComposer.Compose(SampleInput());

        Assert.Contains("niezweryfikowana", RowLine(output, "e5f6a7b8"));
        Assert.Contains("| alive |", RowLine(output, "a1b2c3d4"));
        Assert.DoesNotContain("niezweryfikowana", RowLine(output, "a1b2c3d4"));
    }

    [Fact]
    public void Empty_day_says_so()
    {
        ReportInput input = SampleInput() with { Rows = Array.Empty<ReportRow>() };

        string output = ReportComposer.Compose(input);

        Assert.Contains(EmptyDayLine, output);
        Assert.DoesNotContain(HeaderLine, output);
        Assert.DoesNotContain("|---|", output);
    }

    [Fact]
    public void Pipes_are_escaped()
    {
        ReportRow beta = SampleRows()[1] with { Url = "https://example.invalid/a|b" };
        ReportInput input = SampleInput() with { Rows = new[] { beta } };

        string output = ReportComposer.Compose(input);

        Assert.Contains("| Beta \\| Co |", output);
        Assert.Contains("[link](https://example.invalid/a%7Cb)", output);
        Assert.DoesNotContain("a|b", output);
    }

    [Fact]
    public void Same_input_gives_identical_output()
    {
        string first = ReportComposer.Compose(SampleInput());
        string second = ReportComposer.Compose(SampleInput());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Source_health_alert_is_listed()
    {
        string output = ReportComposer.Compose(SampleInput());

        Assert.Contains("- ashby: 0 (mediana 40) - ALERT: 0 offers found today", output);
        Assert.Contains("- weworkremotely: nie uruchomiono", output);
        Assert.DoesNotContain("- greenhouse: 120 (mediana 118) - ALERT", output);
    }

    [Fact]
    public void Unknown_cost_is_shown_as_unknown()
    {
        string output = ReportComposer.Compose(SampleInput());

        Assert.Contains("- scorer: 1 partii, 3000/200 tokens, nieznany kredytów, nieznany", output);
    }

    private static ReportInput SampleInput() => new(
        RunDate: new DateOnly(2026, 11, 12),
        SummaryMd: "Dziś 3 nowe oferty.",
        Rows: SampleRows(),
        NotEvaluatedCount: 3,
        Sources: new[]
        {
            new SourceHealthLine("greenhouse", 120, 118.0, false, null, false),
            new SourceHealthLine("ashby", 0, 40.0, true, "0 offers found today", false),
            new SourceHealthLine("weworkremotely", 0, null, false, null, true),
        },
        PrefilterCounts: new Dictionary<string, int> { ["title-no-keyword"] = 310, ["mode-onsite"] = 12 },
        Costs: new[]
        {
            new CostLine("extractor", 2, 12000, 800, 1.5, 0.015),
            new CostLine("scorer", 1, 3000, 200, null, null),
        },
        CriteriaVersion: 1,
        PromptVersions: PromptVersionsSample,
        Commit: "abc1234",
        Labels: Labels());

    private static ReportRow[] SampleRows() => new[]
    {
        new ReportRow(new DateOnly(2026, 11, 12), "Acme", "Senior .NET Developer", "https://example.invalid/1", "remote", "B", "20000-30000 PLN/month", ReportStatus.Alive, "a1b2c3d4", 91),
        new ReportRow(new DateOnly(2026, 11, 11), "Beta | Co", "AI Engineer", "https://example.invalid/2", "hybrid", "A", "", ReportStatus.Unverified, "e5f6a7b8", 91),
        new ReportRow(new DateOnly(2026, 11, 12), "Gamma", "Backend Engineer", "https://example.invalid/3", "remote", "A", "", ReportStatus.Alive, "c9d0e1f2", 78),
    };

    private static ReportLabels Labels() => new(
        Columns: ColumnNames,
        StatusAlive: "alive",
        StatusUnverified: "niezweryfikowana",
        EmptyDay: EmptyDayLine,
        NotEvaluated: "Nie ocenione",
        SourceHealth: "Zdrowie źródeł",
        Median: "mediana",
        Alert: "ALERT",
        NotRun: "nie uruchomiono",
        Prefilter: "Prefiltr",
        Cost: "Koszt",
        Unknown: "nieznany",
        Batches: "partii",
        Credits: "kredytów");

    /// <summary>Reads the golden file by walking up from the test binary to the directory that holds JobRadar.sln.</summary>
    private static string ReadGolden()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "JobRadar.sln")))
        {
            string? parent = Path.GetDirectoryName(dir);
            if (parent is null)
            {
                throw new InvalidOperationException("JobRadar.sln not found above the test binary.");
            }

            dir = parent;
        }

        return File.ReadAllText(Path.Combine(dir, "tests", "JobRadar.Core.Tests", "golden", "report-full.md"));
    }

    private static string RowLine(string output, string code) => output
        .Split('\n')
        .Single(line => line.StartsWith("| 20", StringComparison.Ordinal) && line.EndsWith($"| {code} |", StringComparison.Ordinal));

    private static string[] TableCodes(string output) => output
        .Split('\n')
        .Where(line => line.StartsWith("| 20", StringComparison.Ordinal))
        .Select(line => line.Split('|')[^2].Trim())
        .ToArray();
}
