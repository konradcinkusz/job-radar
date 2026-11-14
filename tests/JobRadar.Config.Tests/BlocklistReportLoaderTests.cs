namespace JobRadar.Config.Tests;

public sealed class BlocklistReportLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "jr-config-" + Guid.NewGuid().ToString("N"));

    public BlocklistReportLoaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Blocklist_is_normalised()
    {
        var result = ConfigLoader.LoadBlocklist(Write("blocklist.yaml", """
            companies: ["Body Shop Sp. z o.o.", "ACME GmbH"]
            """));

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains("body shop", result.Value);
        Assert.Contains("acme", result.Value);
    }

    [Fact]
    public void Empty_blocklist_is_valid()
    {
        var real = ConfigLoader.LoadBlocklist(Real("blocklist.yaml"));
        var commentOnly = ConfigLoader.LoadBlocklist(Write("blocklist.yaml", """
            # Nothing blocked yet.
            companies: []
            """));

        Assert.Empty(real.Errors);
        Assert.NotNull(real.Value);
        Assert.Empty(real.Value);
        Assert.Empty(commentOnly.Errors);
        Assert.NotNull(commentOnly.Value);
        Assert.Empty(commentOnly.Value);
    }

    [Fact]
    public void Blocklist_unknown_key_is_an_error()
    {
        var result = ConfigLoader.LoadBlocklist(Write("blocklist.yaml", """
            companies: []
            bogus: 1
            """));

        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal("blocklist.yaml", error.File);
        Assert.Equal("bogus", error.Path);
        Assert.Equal("unknown key", error.Rule);
    }

    [Fact]
    public void Blocklist_entry_empty_after_normalisation_is_an_error()
    {
        var result = ConfigLoader.LoadBlocklist(Write("blocklist.yaml", """
            companies: ["Sp. z o.o."]
            """));

        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal("companies[0]", error.Path);
        Assert.Equal("must not be empty after normalisation", error.Rule);
    }

    [Fact]
    public void Report_seed_loads_with_polish_labels()
    {
        var result = ConfigLoader.LoadReport(Real("report.yaml"));

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Value);
        Assert.Equal(70, result.Value.Threshold);
        Assert.False(result.Value.RequireScore);
        Assert.Equal(22, result.Value.Labels.Count);
        Assert.Equal("ścieżka", result.Value.Labels["column_track"]);
        Assert.Equal("niezweryfikowana", result.Value.Labels["status_unverified"]);
        Assert.Equal("Brak nowych ofert.", result.Value.Labels["empty_day"]);
    }

    [Theory]
    [InlineData("101")]
    [InlineData("-1")]
    public void Report_threshold_out_of_range_is_an_error(string value)
    {
        var result = ConfigLoader.LoadReport(Write("report.yaml",
            Mutate(RealText("report.yaml"), "threshold: 70", $"threshold: {value}")));

        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal("report.yaml", error.File);
        Assert.Equal("threshold", error.Path);
        Assert.Equal("must be an integer between 0 and 100", error.Rule);
    }

    [Fact]
    public void Report_unknown_key_is_an_error_naming_the_path()
    {
        var topLevel = ConfigLoader.LoadReport(Write("report.yaml",
            Mutate(RealText("report.yaml"), "require_score: false", "require_score: false\nbogus: 1")));
        var inLabels = ConfigLoader.LoadReport(Write("report.yaml",
            Mutate(RealText("report.yaml"), "  credits: kredytów", "  credits: kredytów\n  bogus: x")));

        Assert.Null(topLevel.Value);
        Assert.Contains(topLevel.Errors, e => e.File == "report.yaml" && e.Path == "bogus" && e.Rule == "unknown key");
        Assert.Null(inLabels.Value);
        Assert.Contains(inLabels.Errors, e => e.File == "report.yaml" && e.Path == "labels.bogus" && e.Rule == "unknown key");
    }

    [Fact]
    public void Report_missing_label_is_an_error()
    {
        var result = ConfigLoader.LoadReport(Write("report.yaml",
            Mutate(RealText("report.yaml"), "  credits: kredytów\n", "")));

        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal("labels.credits", error.Path);
        Assert.Equal("missing label", error.Rule);
    }

    [Fact]
    public void Report_empty_label_value_is_an_error()
    {
        var result = ConfigLoader.LoadReport(Write("report.yaml",
            Mutate(RealText("report.yaml"), "  empty_day: Brak nowych ofert.", "  empty_day: \"\"")));

        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal("labels.empty_day", error.Path);
        Assert.Equal("must be a non-empty string", error.Rule);
    }

    [Fact]
    public void Missing_file_is_an_error_not_an_exception()
    {
        var missing = Path.Combine(_dir, "nope.yaml");

        var blocklist = ConfigLoader.LoadBlocklist(missing);
        var report = ConfigLoader.LoadReport(missing);

        Assert.Null(blocklist.Value);
        Assert.Equal("file not found", Assert.Single(blocklist.Errors).Rule);
        Assert.Null(report.Value);
        Assert.Equal("file not found", Assert.Single(report.Errors).Rule);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "JobRadar.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("JobRadar.sln not found above the test binaries");
    }

    private static string Real(string name) => Path.Combine(RepoRoot(), "config", name);

    private static string RealText(string name) => File.ReadAllText(Real(name));

    private static string Mutate(string text, string from, string to)
    {
        Assert.Contains(from, text, StringComparison.Ordinal);
        return text.Replace(from, to, StringComparison.Ordinal);
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }
}
