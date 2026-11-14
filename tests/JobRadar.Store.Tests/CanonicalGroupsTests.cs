using JobRadar.Core.Models;
using JobRadar.Core.Ports;
using JobRadar.Store;
using JobRadar.Store.Model;
using JobRadar.Store.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Store.Tests;

/// <summary>Canonical groups and primary choice on a real SQLite file (never the InMemory provider).</summary>
public sealed class CanonicalGroupsTests : IDisposable
{
    private const string Key = "synthetic-key";
    private const string Ats = "synthetic-ats";
    private const string Portal = "synthetic-portal";
    private const string Aggregator = "synthetic-aggregator";

    private static readonly FakeKinds Kinds = new(new Dictionary<string, SourceKind>(StringComparer.Ordinal)
    {
        [Ats] = SourceKind.Ats,
        [Portal] = SourceKind.Portal,
        [Aggregator] = SourceKind.Aggregator,
    });

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
    public async Task Ats_beats_portal_beats_aggregator()
    {
        var path = await MigratedPathAsync();
        await InsertAsync(path,
            Row("a-agg", Aggregator, Key, "2026-11-01"),
            Row("b-portal", Portal, Key, "2026-11-01"),
            Row("c-ats", Ats, Key, "2026-11-10"));

        var changed = await RecomputeAsync(path, Key);

        // The ATS row was already NULL (its own primary), so only the two others change.
        Assert.Equal(2, changed);
        var rows = await ReadAsync(path);
        Assert.Null(rows["c-ats"].PrimaryOfferId);
        Assert.Equal("c-ats", rows["a-agg"].PrimaryOfferId);
        Assert.Equal("c-ats", rows["b-portal"].PrimaryOfferId);
    }

    [Fact]
    public async Task Tie_goes_to_earliest_first_seen()
    {
        var path = await MigratedPathAsync();
        // The earlier-seen row has the LARGER id, so ordering by id first would pick the wrong primary.
        await InsertAsync(path,
            Row("zzz-early", Ats, Key, "2026-11-03"),
            Row("aaa-late", Ats, Key, "2026-11-12"));

        await RecomputeAsync(path, Key);

        var rows = await ReadAsync(path);
        Assert.Null(rows["zzz-early"].PrimaryOfferId);
        Assert.Equal("zzz-early", rows["aaa-late"].PrimaryOfferId);
    }

    [Fact]
    public async Task Second_tie_goes_to_smaller_id()
    {
        var path = await MigratedPathAsync();
        // Separate inserts so that "bbb" is stored first: without an id tie-break the database order would pick "bbb".
        await InsertAsync(path, Row("bbb", Ats, Key, "2026-11-05"));
        await InsertAsync(path, Row("aaa", Ats, Key, "2026-11-05"));

        await RecomputeAsync(path, Key);

        var rows = await ReadAsync(path);
        Assert.Null(rows["aaa"].PrimaryOfferId);
        Assert.Equal("aaa", rows["bbb"].PrimaryOfferId);
    }

    [Fact]
    public async Task Single_offer_is_its_own_primary()
    {
        var path = await MigratedPathAsync();
        await InsertAsync(path, Row("solo", Portal, Key, "2026-11-05"));

        var changed = await RecomputeAsync(path, Key);

        Assert.Equal(0, changed);
        var rows = await ReadAsync(path);
        Assert.Null(rows["solo"].PrimaryOfferId);
    }

    [Fact]
    public async Task Recompute_is_idempotent()
    {
        var path = await MigratedPathAsync();
        await InsertAsync(path,
            Row("a-agg", Aggregator, Key, "2026-11-01"),
            Row("b-ats", Ats, Key, "2026-11-02"));

        var first = await RecomputeAsync(path, Key);
        var afterFirst = await ReadAsync(path);

        var second = await RecomputeAsync(path, Key);
        var afterSecond = await ReadAsync(path);

        // Only the aggregator row changes (NULL -> "b-ats"); the ATS row was already its own primary.
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(afterFirst.Select(r => (r.Key, r.Value.PrimaryOfferId)), afterSecond.Select(r => (r.Key, r.Value.PrimaryOfferId)));
    }

    [Fact]
    public async Task A_new_better_source_takes_over_the_primary()
    {
        var path = await MigratedPathAsync();
        await InsertAsync(path, Row("portal", Portal, Key, "2026-11-01"));
        await RecomputeAsync(path, Key);
        Assert.Null((await ReadAsync(path))["portal"].PrimaryOfferId);

        await InsertAsync(path, Row("ats", Ats, Key, "2026-11-14"));
        await RecomputeAsync(path, Key);

        var rows = await ReadAsync(path);
        Assert.Null(rows["ats"].PrimaryOfferId);
        Assert.Equal("ats", rows["portal"].PrimaryOfferId);
    }

    [Fact]
    public async Task Different_keys_do_not_interact()
    {
        var path = await MigratedPathAsync();
        await InsertAsync(path,
            Row("k1-ats", Ats, "key-one", "2026-11-01"),
            Row("k1-portal", Portal, "key-one", "2026-11-01"),
            Row("k2-portal", Portal, "key-two", "2026-11-01"),
            Row("k2-agg", Aggregator, "key-two", "2026-11-02"));

        var changed = await RecomputeAsync(path, "key-one");

        Assert.Equal(1, changed);
        var rows = await ReadAsync(path);
        Assert.Null(rows["k1-ats"].PrimaryOfferId);
        Assert.Equal("k1-ats", rows["k1-portal"].PrimaryOfferId);
        Assert.Null(rows["k2-portal"].PrimaryOfferId);
        Assert.Null(rows["k2-agg"].PrimaryOfferId);
    }

    /// <summary>A synthetic offer row; every NOT NULL column has a fixed synthetic value.</summary>
    private static OfferRow Row(string id, string source, string canonicalKey, string firstSeenAt) => new()
    {
        Id = id,
        Source = source,
        SourceId = id,
        Url = "https://example.org/jobs/" + id,
        CanonicalKey = canonicalKey,
        PrimaryOfferId = null,
        Title = "Synthetic Engineer",
        Company = "Synthetic Co",
        WorkMode = "remote",
        Description = "Synthetic posting text.",
        DescriptionHash = "synthetic-hash",
        FirstSeenAt = firstSeenAt,
        LastSeenAt = firstSeenAt,
        Lifecycle = "new",
        MissedRuns = 0,
        Liveness = "unknown",
        NeedsReview = false,
    };

    private static async Task InsertAsync(string path, params OfferRow[] rows)
    {
        await using var db = Open(path);
        db.Offers.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private static async Task<int> RecomputeAsync(string path, params string[] keys)
    {
        await using var db = Open(path);
        return await new CanonicalGroups(db, Kinds).RecomputeAsync(keys, default);
    }

    private static async Task<Dictionary<string, OfferRow>> ReadAsync(string path)
    {
        await using var db = Open(path);
        return (await db.Offers.AsNoTracking().ToListAsync()).ToDictionary(o => o.Id, StringComparer.Ordinal);
    }

    private static JobRadarDbContext Open(string path) =>
        new(new DbContextOptionsBuilder<JobRadarDbContext>().UseSqlite("Data Source=" + path).Options);

    private sealed class FakeKinds(Dictionary<string, SourceKind> map) : ISourceKinds
    {
        public SourceKind KindOf(string source) => map[source];
    }
}
