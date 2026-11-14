using JobRadar.Core.Models;

namespace JobRadar.Config.Tests;

public sealed class ConfigLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "jr-config-" + Guid.NewGuid().ToString("N"));

    public ConfigLoaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Seed_files_load_without_errors()
    {
        var sources = ConfigLoader.LoadSources(Real("sources.yaml"));
        var criteria = ConfigLoader.LoadCriteria(Real("criteria.yaml"));

        Assert.Empty(sources.Errors);
        Assert.Empty(criteria.Errors);
        Assert.NotNull(sources.Value);
        Assert.NotNull(criteria.Value);
        Assert.Equal(1, criteria.Value.Version);
        Assert.Equal(70, criteria.Value.Threshold);
        Assert.False(criteria.Value.Tracks[Track.C].Enabled);
        Assert.Equal(SourceKind.Ats, sources.Value.Single(s => s.Id == "greenhouse").Kind);
        Assert.Equal("Remotive", sources.Value.Single(s => s.Id == "remotive").Attribution);
    }

    [Fact]
    public void Unknown_key_is_an_error_naming_the_path()
    {
        var criteria = ConfigLoader.LoadCriteria(Write("criteria.yaml", Mutate(RealText("criteria.yaml"), "version: 1\n", "version: 1\nbogus: 1\n")));
        var sources = ConfigLoader.LoadSources(Write("sources.yaml", """
            sources:
              greenhouse: { enabled: true, kind: ats, stage: 0, expiry_by_absence: true, bogus: 1 }
            """));

        Assert.Null(criteria.Value);
        Assert.Contains(criteria.Errors, e => e.File == "criteria.yaml" && e.Path == "bogus" && e.Rule == "unknown key");
        Assert.Null(sources.Value);
        Assert.Contains(sources.Errors, e => e.File == "sources.yaml" && e.Path == "sources.greenhouse.bogus" && e.Rule == "unknown key");
    }

    [Fact]
    public void Linkedin_source_is_refused_citing_D03()
    {
        var result = ConfigLoader.LoadSources(Write("sources.yaml", """
            sources:
              linkedin: { enabled: false, kind: portal, stage: 0, expiry_by_absence: false }
            """));

        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal("sources.linkedin", error.Path);
        Assert.Contains("'linkedin'", error.Rule);
        Assert.Contains("D-03", error.Rule);
    }

    [Fact]
    public void Indeed_source_is_refused_citing_D03()
    {
        var result = ConfigLoader.LoadSources(Write("sources.yaml", """
            sources:
              indeed: { enabled: false, kind: portal, stage: 0, expiry_by_absence: false }
            """));

        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal("sources.indeed", error.Path);
        Assert.Contains("'indeed'", error.Rule);
        Assert.Contains("D-03", error.Rule);
    }

    [Fact]
    public void Missing_file_is_an_error_not_an_exception()
    {
        var missing = Path.Combine(_dir, "nope.yaml");

        var sources = ConfigLoader.LoadSources(missing);
        var criteria = ConfigLoader.LoadCriteria(missing);

        Assert.Null(sources.Value);
        Assert.Equal("nope.yaml", Assert.Single(sources.Errors).File);
        Assert.Equal("file not found", Assert.Single(sources.Errors).Rule);
        Assert.Null(criteria.Value);
        Assert.Equal("file not found", Assert.Single(criteria.Errors).Rule);
    }

    [Fact]
    public void Bad_track_letter_is_an_error()
    {
        var result = ConfigLoader.LoadCriteria(Write("criteria.yaml",
            Mutate(RealText("criteria.yaml"), "  C: { enabled: false", "  D: { enabled: false")));

        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal("tracks.D", error.Path);
        Assert.Equal("track must be A, B or C", error.Rule);
    }

    [Fact]
    public void Source_id_pattern_is_enforced()
    {
        var result = ConfigLoader.LoadSources(Write("sources.yaml", """
            sources:
              Bad_Id: { enabled: true, kind: ats, stage: 0, expiry_by_absence: true }
            """));

        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal("sources.Bad_Id", error.Path);
        Assert.Equal("must match ^[a-z][a-z0-9-]{1,30}$", error.Rule);
    }

    [Fact]
    public void Bad_slug_is_an_error()
    {
        var result = ConfigLoader.LoadSources(Write("sources.yaml", """
            sources:
              greenhouse: { enabled: true, kind: ats, stage: 0, expiry_by_absence: true, companies: [{ slug: "bad slug!" }] }
            """));

        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal("sources.greenhouse.companies[0].slug", error.Path);
        Assert.Equal("must match ^[A-Za-z0-9._-]+$", error.Rule);
    }

    [Fact]
    public void Threshold_over_100_is_an_error()
    {
        var result = ConfigLoader.LoadCriteria(Write("criteria.yaml",
            Mutate(RealText("criteria.yaml"), "threshold: 70", "threshold: 101")));

        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal("threshold", error.Path);
        Assert.Equal("must be an integer between 0 and 100", error.Rule);
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
