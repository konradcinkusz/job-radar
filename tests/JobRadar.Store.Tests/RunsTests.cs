using System.Globalization;
using JobRadar.Store;
using JobRadar.Store.Model;
using JobRadar.Store.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Store.Tests;

/// <summary>The <c>runs</c> writer and the healthy-count reader on a real SQLite file (never the InMemory provider).</summary>
public sealed class RunsTests : IDisposable
{
    private readonly List<string> _files = [];

    // xUnit creates one instance per test, so every test gets its own file and removes it in Dispose.
    private async Task<string> MigratedPathAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db");
        _files.Add(path);
        await Migrate.RunAsync(path);
        return path;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in _files)
        {
            foreach (var candidate in new[] { file, file + "-journal", file + "-wal", file + "-shm" })
            {
                if (File.Exists(candidate))
                {
                    File.Delete(candidate);
                }
            }
        }
    }

    [Fact]
    public async Task RecordAsync_twice_replaces_the_row()
    {
        var path = await MigratedPathAsync();
        var first = Run("run-1", "greenhouse", "scrape", day: 10, healthy: true, offersFound: 10, errors: ["timeout"]);
        var second = Run("run-1", "greenhouse", "scrape", day: 10, healthy: false, offersFound: 12, errors: []);

        await RecordAsync(path, first);
        await using (var afterFirst = Open(path))
        {
            var row = await afterFirst.Runs.AsNoTracking().SingleAsync();
            Assert.Equal("[\"timeout\"]", row.Errors);
            Assert.Equal(10, row.OffersFound);
            Assert.True(row.Healthy);
        }

        await RecordAsync(path, second);

        await using var db = Open(path);
        var rows = await db.Runs.AsNoTracking().ToListAsync();
        var replaced = Assert.Single(rows);
        Assert.Equal("run-1", replaced.RunId);
        Assert.Equal(12, replaced.OffersFound);
        Assert.False(replaced.Healthy);
        Assert.Equal("[]", replaced.Errors);
        Assert.Equal(second.FinishedAt, replaced.FinishedAt);
    }

    [Fact]
    public async Task LastHealthyCounts_skips_unhealthy_runs()
    {
        var path = await MigratedPathAsync();
        // The skipped runs are placed among the newest ones: a filter that is missing would then change the result of take: 3.
        await RecordAsync(path,
            Run("run-01", "greenhouse", "scrape", day: 1, healthy: true, offersFound: 10),
            Run("run-02", "greenhouse", "scrape", day: 2, healthy: true, offersFound: 12),
            Run("run-03", "greenhouse", "scrape", day: 3, healthy: true, offersFound: 11),
            Run("run-04", "greenhouse", "enrich", day: 4, healthy: true, offersFound: 500),
            Run("run-05", "lever", "scrape", day: 5, healthy: true, offersFound: 700),
            Run("run-06", "greenhouse", "scrape", day: 6, healthy: false, offersFound: 99),
            Run("run-07", "greenhouse", "scrape", day: 7, healthy: true, offersFound: 15),
            Run("run-08", "greenhouse", "scrape", day: 8, healthy: true, offersFound: 13));

        var counts = await LastHealthyCountsAsync(path, "greenhouse", take: 3);

        // Newest first among healthy greenhouse scrape runs: run-08 (13), run-07 (15), run-03 (11).
        // Skipped: run-06 (unhealthy, 99), run-04 (enrich, 500), run-05 (lever, 700).
        Assert.Equal([13, 15, 11], counts);
    }

    private static RunRecord Run(
        string runId,
        string source,
        string kind,
        int day,
        bool healthy,
        int offersFound,
        IReadOnlyList<string>? errors = null) => new(
        RunId: runId,
        Source: source,
        Kind: kind,
        StartedAt: Stamp(day, 8),
        FinishedAt: Stamp(day, 9),
        OffersFound: offersFound,
        OffersNew: 0,
        OffersChanged: 0,
        Errors: errors ?? Array.Empty<string>(),
        Healthy: healthy);

    private static string Stamp(int day, int hour) =>
        new DateTimeOffset(2026, 11, day, hour, 0, 0, TimeSpan.Zero).ToString("o", CultureInfo.InvariantCulture);

    private static async Task RecordAsync(string path, params RunRecord[] runs)
    {
        await using var db = Open(path);
        var runsWriter = new Runs(db);
        foreach (var run in runs)
        {
            await runsWriter.RecordAsync(run);
        }
    }

    private static async Task<IReadOnlyList<int>> LastHealthyCountsAsync(string path, string source, int take)
    {
        await using var db = Open(path);
        return await new Runs(db).LastHealthyCountsAsync(source, take);
    }

    private static JobRadarDbContext Open(string path) =>
        new(new DbContextOptionsBuilder<JobRadarDbContext>().UseSqlite("Data Source=" + path).Options);
}
