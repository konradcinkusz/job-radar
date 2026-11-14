using System.Globalization;
using System.Text.Json.Nodes;
using JobRadar.Cli.Stages;
using JobRadar.Core.Identity;
using JobRadar.Core.Models;
using JobRadar.Core.Ports;
using Microsoft.Data.Sqlite;

namespace JobRadar.Cli.Tests;

public class IngestTests
{
    private static readonly DateOnly Day1 = new(2026, 11, 12);

    [Fact]
    public async Task Ingest_twice_gives_identical_database_rows()
    {
        using var ws = new Workspace();
        ws.WriteRaw([Offer("gh-1"), Offer("gh-2"), Offer("gh-3", title: "Office Manager")]);
        ws.WriteSummary(Healthy(true));

        Assert.Equal(0, (await Run(ws, Day1)).Code);
        var first = Snapshot(ws.DbPath);

        var second = await Run(ws, Day1);

        Assert.Equal(0, second.Code);
        Assert.Equal(first, Snapshot(ws.DbPath));
        Assert.Empty(IdList(ws.ResultPath, "new_ids"));
    }

    [Fact]
    public async Task Prefilter_reason_is_stored_and_counted()
    {
        using var ws = new Workspace();
        ws.WriteRaw(
        [
            Offer("gh-pass"),
            Offer("gh-title", title: "Office Manager"),
            Offer("gh-onsite", title: "Backend Engineer", mode: WorkMode.Onsite),
        ]);
        ws.WriteSummary(Healthy(true));

        var run = await Run(ws, Day1);

        Assert.Equal(0, run.Code);
        var reasons = Rows(ws.DbPath, "SELECT id, prefilter_reason FROM offers")
            .ToDictionary(r => (string)r["id"]!, r => (string?)r["prefilter_reason"], StringComparer.Ordinal);
        Assert.Null(reasons[OfferId.Of("greenhouse", "gh-pass")]);
        Assert.Equal("title-no-keyword", reasons[OfferId.Of("greenhouse", "gh-title")]);
        Assert.Equal("mode-onsite", reasons[OfferId.Of("greenhouse", "gh-onsite")]);

        var json = Result(ws);
        Assert.Equal(1, json["prefilter_reasons"]!["title-no-keyword"]!.GetValue<int>());
        Assert.Equal(1, json["prefilter_reasons"]!["mode-onsite"]!.GetValue<int>());
        Assert.Equal(1, json["passed_prefilter"]!.GetValue<int>());
    }

    [Fact]
    public async Task Unhealthy_source_does_not_expire_its_offers()
    {
        using var ws = new Workspace();
        ws.WriteRaw([Offer("gh-1"), Offer("gh-2")]);
        ws.WriteSummary(Healthy(true));
        Assert.Equal(0, (await Run(ws, Day1)).Code);

        ws.WriteRaw([]);
        ws.WriteSummary(Healthy(false));
        var exits = new List<int>();
        foreach (var day in new[] { 13, 14, 15 })
        {
            exits.Add((await Run(ws, new DateOnly(2026, 11, day))).Code);
        }

        Assert.All(exits, code => Assert.Equal(0, code));
        var rows = Rows(ws.DbPath, "SELECT lifecycle, missed_runs FROM offers");
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal("new", r["lifecycle"]);
            Assert.Equal(0L, r["missed_runs"]);
        });
    }

    [Fact]
    public async Task Healthy_source_missing_three_days_expires_its_offers()
    {
        using var ws = new Workspace();
        ws.WriteRaw([Offer("gh-1"), Offer("gh-2")]);
        ws.WriteSummary(Healthy(true));
        Assert.Equal(0, (await Run(ws, Day1)).Code);

        ws.WriteRaw([]);
        Assert.Equal(0, (await Run(ws, new DateOnly(2026, 11, 13))).Code);
        Assert.Equal(0, (await Run(ws, new DateOnly(2026, 11, 14))).Code);
        Assert.All(Rows(ws.DbPath, "SELECT lifecycle, missed_runs FROM offers"), r =>
        {
            Assert.Equal("new", r["lifecycle"]);
            Assert.Equal(2L, r["missed_runs"]);
        });

        var third = await Run(ws, new DateOnly(2026, 11, 15));

        Assert.Equal(0, third.Code);
        Assert.Equal(2, SourceResult(ws, "greenhouse")["expired"]!.GetValue<int>());
        Assert.All(Rows(ws.DbPath, "SELECT lifecycle, missed_runs FROM offers"), r =>
        {
            Assert.Equal("expired", r["lifecycle"]);
            Assert.Equal(3L, r["missed_runs"]);
        });
    }

    [Fact]
    public async Task Result_json_lists_new_and_changed_ids()
    {
        using var ws = new Workspace();
        ws.WriteRaw([Offer("gh-1"), Offer("gh-2"), Offer("gh-3")]);
        ws.WriteSummary(Healthy(true));

        Assert.Equal(0, (await Run(ws, Day1)).Code);
        var day1 = IdList(ws.ResultPath, "new_ids");
        Assert.Equal(
            new[] { OfferId.Of("greenhouse", "gh-1"), OfferId.Of("greenhouse", "gh-2"), OfferId.Of("greenhouse", "gh-3") }.Order(StringComparer.Ordinal),
            day1);

        ws.WriteRaw([Offer("gh-1"), Offer("gh-2", description: "Build services, now with more detail."), Offer("gh-3")]);
        Assert.Equal(0, (await Run(ws, new DateOnly(2026, 11, 13))).Code);

        Assert.Empty(IdList(ws.ResultPath, "new_ids"));
        Assert.Equal(new[] { OfferId.Of("greenhouse", "gh-2") }, IdList(ws.ResultPath, "changed_ids"));
    }

    [Fact]
    public async Task Ratio_is_printed_and_in_the_result_file()
    {
        using var ws = new Workspace();
        ws.WriteRaw(
        [
            Offer("gh-pass"),
            Offer("gh-title", title: "Office Manager"),
            Offer("gh-onsite", title: "Backend Engineer", mode: WorkMode.Onsite),
        ]);
        ws.WriteSummary(Healthy(true));

        var run = await Run(ws, Day1);

        Assert.Equal(0, run.Code);
        Assert.Contains("ratio 0.3333", run.Output, StringComparison.Ordinal);
        Assert.Contains("ingest: 3 ingested, 1 passed prefilter (ratio 0.3333), 3 new, 0 changed, 0 expired, 0 archived", run.Output, StringComparison.Ordinal);
        var text = File.ReadAllText(ws.ResultPath);
        Assert.Contains("\"ratio_passed_to_ingested\": 0.3333", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Config_error_exits_2_and_changes_nothing()
    {
        using var ws = new Workspace();
        File.WriteAllText(Path.Combine(ws.ConfigDir, "sources.yaml"), "sources: [unclosed\n");
        ws.WriteRaw([Offer("gh-1")]);

        var run = await Run(ws, Day1);

        Assert.Equal(2, run.Code);
        Assert.StartsWith("config error: sources.yaml", run.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(ws.DbPath));
        Assert.False(File.Exists(ws.ResultPath));
    }

    [Fact]
    public async Task Bad_json_line_is_counted_and_skipped()
    {
        using var ws = new Workspace();
        ws.WriteRawLines(
        [
            RawOfferJson.ToLine(Offer("gh-1")),
            "{ this is not json",
            RawOfferJson.ToLine(Offer("gh-2") with { Source = "ashby" }),
        ]);
        ws.WriteSummary(Healthy(true));

        var run = await Run(ws, Day1);

        Assert.Equal(0, run.Code);
        var greenhouse = SourceResult(ws, "greenhouse");
        Assert.Equal("greenhouse", greenhouse["source"]!.GetValue<string>());
        Assert.Equal(1, greenhouse["ingested"]!.GetValue<int>());
        Assert.Equal(2, greenhouse["errors"]!.GetValue<int>());
        Assert.Single(Rows(ws.DbPath, "SELECT id FROM offers"));
    }

    [Fact]
    public async Task Missing_summary_means_unhealthy()
    {
        using var ws = new Workspace();
        ws.WriteRaw([Offer("gh-1"), Offer("gh-2")]);
        Assert.Equal(0, (await Run(ws, Day1)).Code);

        ws.WriteRaw([]);
        foreach (var day in new[] { 13, 14, 15 })
        {
            Assert.Equal(0, (await Run(ws, new DateOnly(2026, 11, day))).Code);
        }

        Assert.False(SourceResult(ws, "greenhouse")["healthy"]!.GetValue<bool>());
        Assert.All(Rows(ws.DbPath, "SELECT lifecycle, missed_runs FROM offers"), r =>
        {
            Assert.Equal("new", r["lifecycle"]);
            Assert.Equal(0L, r["missed_runs"]);
        });
    }

    [Fact]
    public async Task Hooks_run_after_the_upsert_with_the_new_ids()
    {
        using var ws = new Workspace();
        ws.WriteRaw([Offer("gh-1"), Offer("gh-2")]);
        ws.WriteSummary(Healthy(true));
        var hook = new RecordingHook();

        var run = await Run(ws, Day1, hook);

        Assert.Equal(0, run.Code);
        Assert.Single(hook.Contexts);
        Assert.Equal(Day1, hook.Contexts[0].RunDate);
        Assert.Equal(ws.DataDir, hook.Contexts[0].DataDir);
        Assert.Equal(
            new[] { OfferId.Of("greenhouse", "gh-1"), OfferId.Of("greenhouse", "gh-2") }.Order(StringComparer.Ordinal),
            hook.Contexts[0].NewIds);
    }

    // ---- helpers -------------------------------------------------------------------------------------------------

    private static RawOffer Offer(
        string sourceId,
        string title = "Senior .NET Developer",
        WorkMode mode = WorkMode.Remote,
        string description = "Build services.") =>
        new("greenhouse", sourceId, $"https://example.test/jobs/{sourceId}", title, "Acme Labs", "DE", "Berlin",
            mode, null, "B2B", null, null, null, null, description, null, null);

    private static string Healthy(bool healthy) =>
        "{\"run_date\":\"2026-11-12\",\"sources\":[{\"source\":\"greenhouse\",\"healthy\":" +
        (healthy ? "true" : "false") + ",\"offers_found\":0,\"errors\":[]}]}";

    private static async Task<(int Code, string Output)> Run(Workspace ws, DateOnly runDate, params IIngestHook[] hooks)
    {
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var stage = new IngestStage(hooks, output);
        var code = await stage.RunAsync(new IngestOptions(ws.DataDir, ws.ConfigDir, runDate), CancellationToken.None);
        return (code, output.ToString());
    }

    private static JsonObject Result(Workspace ws) =>
        JsonNode.Parse(File.ReadAllText(ws.ResultPath))!.AsObject();

    private static JsonNode SourceResult(Workspace ws, string source) =>
        Result(ws)["sources"]!.AsArray().Single(s => s!["source"]!.GetValue<string>() == source)!;

    private static List<string> IdList(string resultPath, string key) =>
        JsonNode.Parse(File.ReadAllText(resultPath))![key]!.AsArray().Select(n => n!.GetValue<string>()).ToList();

    /// <summary>Every column of every <c>offers</c> row, in id order, as text: equal snapshots mean equal rows.</summary>
    private static List<string> Snapshot(string dbPath) =>
        Rows(dbPath, "SELECT * FROM offers ORDER BY id")
            .Select(r => string.Join("|", r.Select(kv => kv.Key + "=" + (kv.Value is null ? "NULL" : Convert.ToString(kv.Value, CultureInfo.InvariantCulture)))))
            .ToList();

    private static List<Dictionary<string, object?>> Rows(string dbPath, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();

        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    private sealed class RecordingHook : IIngestHook
    {
        public List<IngestContext> Contexts { get; } = [];

        public Task RunAsync(IngestContext context, CancellationToken ct)
        {
            Contexts.Add(context);
            return Task.CompletedTask;
        }
    }

    /// <summary>A temp data folder with a copy of the repository's real <c>config/*.yaml</c>. Deleted on dispose.</summary>
    private sealed class Workspace : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "jobradar-ingest-" + Guid.NewGuid().ToString("N"));

        public Workspace()
        {
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(Path.Combine(DataDir, "raw"));
            Directory.CreateDirectory(ConfigDir);
            foreach (var file in Directory.GetFiles(Path.Combine(FindRepositoryRoot(), "config"), "*.yaml"))
            {
                File.Copy(file, Path.Combine(ConfigDir, Path.GetFileName(file)));
            }
        }

        public string DataDir => Path.Combine(_root, "data");

        public string ConfigDir => Path.Combine(_root, "config");

        public string DbPath => Path.Combine(DataDir, "jobradar.db");

        public string ResultPath => Path.Combine(DataDir, "ingest-result.json");

        public void WriteRaw(IEnumerable<RawOffer> offers) =>
            File.WriteAllLines(Path.Combine(DataDir, "raw", "greenhouse.jsonl"), offers.Select(RawOfferJson.ToLine));

        public void WriteRawLines(IEnumerable<string> lines) =>
            File.WriteAllLines(Path.Combine(DataDir, "raw", "greenhouse.jsonl"), lines);

        public void WriteSummary(string json) =>
            File.WriteAllText(Path.Combine(DataDir, "scrape-summary.json"), json);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "JobRadar.sln")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("JobRadar.sln not found above the test binaries");
        }
    }
}
