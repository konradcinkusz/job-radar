using JobRadar.Store;
using JobRadar.Store.Model;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Store.Tests;

/// <summary>Schema, constraints and FTS5 on a real SQLite file (never the InMemory provider).</summary>
public sealed class SchemaTests : IDisposable
{
    private readonly List<string> _files = [];

    // xUnit creates one instance per test, so every test gets its own file and removes it in Dispose.
    private string NewDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db");
        _files.Add(path);
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
    public async Task Migrations_apply_to_an_empty_file()
    {
        var path = NewDbPath();
        File.WriteAllBytes(path, []);
        Assert.Equal(0, new FileInfo(path).Length);

        await Migrate.RunAsync(path);

        var tables = await ObjectNames(path, "table");
        foreach (var expected in new[] { "offers", "offer_fields", "scores", "runs", "agent_usage", "feedback", "offers_fts" })
        {
            Assert.Contains(expected, tables);
        }
    }

    [Fact]
    public async Task Unique_source_pair_is_enforced()
    {
        var path = NewDbPath();
        await Migrate.RunAsync(path);
        await using var db = Open(path);

        db.Offers.Add(Offer("first", "42", "Platform Engineer"));
        await db.SaveChangesAsync();

        // Same (source, source_id), different id: must be refused by the database.
        db.Offers.Add(Offer("second", "42", "Platform Engineer"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Indexes_exist_on_canonical_key_and_lifecycle()
    {
        var path = NewDbPath();
        await Migrate.RunAsync(path);

        var indexes = await OffersIndexNames(path);
        Assert.Contains("ix_offers_canonical_key", indexes);
        Assert.Contains("ix_offers_lifecycle", indexes);
        Assert.Contains("ux_offers_source_source_id", indexes);

        Assert.Equal(["canonical_key"], await IndexColumns(path, "ix_offers_canonical_key"));
        Assert.Equal(["lifecycle"], await IndexColumns(path, "ix_offers_lifecycle"));
    }

    [Fact]
    public async Task Fts_finds_an_inserted_offer_by_title_word()
    {
        var path = NewDbPath();
        await Migrate.RunAsync(path);
        await using var db = Open(path);

        db.Offers.Add(Offer("a", "1", "Platform Engineer"));
        await db.SaveChangesAsync();

        Assert.Contains(await RowIdOf(db, "a"), await Matches(db, "platform"));
    }

    [Fact]
    public async Task Fts_reflects_an_update()
    {
        var path = NewDbPath();
        await Migrate.RunAsync(path);
        await using var db = Open(path);

        db.Offers.Add(Offer("a", "1", "Alpha Engineer"));
        await db.SaveChangesAsync();
        var rowId = await RowIdOf(db, "a");
        Assert.Contains(rowId, await Matches(db, "alpha"));

        var row = await db.Offers.SingleAsync(o => o.Id == "a");
        row.Title = "Beta Engineer";
        await db.SaveChangesAsync();

        Assert.Empty(await Matches(db, "alpha"));
        Assert.Contains(rowId, await Matches(db, "beta"));
    }

    [Fact]
    public async Task Fts_reflects_a_delete()
    {
        var path = NewDbPath();
        await Migrate.RunAsync(path);
        await using var db = Open(path);

        var row = Offer("a", "1", "Gamma Engineer");
        db.Offers.Add(row);
        await db.SaveChangesAsync();
        Assert.Contains(await RowIdOf(db, "a"), await Matches(db, "gamma"));

        db.Offers.Remove(row);
        await db.SaveChangesAsync();

        Assert.Empty(await Matches(db, "gamma"));
    }

    [Fact]
    public async Task Migrations_are_idempotent_when_run_twice()
    {
        var path = NewDbPath();
        await Migrate.RunAsync(path);
        var schemaBefore = await SchemaSnapshot(path);

        await Migrate.RunAsync(path);

        Assert.Equal(schemaBefore, await SchemaSnapshot(path));
        Assert.Equal(2, await AppliedMigrationCount(path));
    }

    private static JobRadarDbContext Open(string path) =>
        new(new DbContextOptionsBuilder<JobRadarDbContext>().UseSqlite("Data Source=" + path).Options);

    private static OfferRow Offer(string id, string sourceId, string title) => new()
    {
        Id = id,
        Source = "greenhouse",
        SourceId = sourceId,
        Url = "https://example.org/jobs/" + sourceId,
        CanonicalKey = "acme|" + title.ToLowerInvariant() + "|PL",
        Title = title,
        Company = "Acme",
        WorkMode = "remote",
        Description = "Synthetic posting text",
        DescriptionHash = "hash-" + id,
        FirstSeenAt = "2026-11-13T00:00:00Z",
        LastSeenAt = "2026-11-13T00:00:00Z",
        Lifecycle = "new",
        MissedRuns = 0,
        Liveness = "unknown",
        NeedsReview = false,
    };

    private static Task<long> RowIdOf(JobRadarDbContext db, string id) =>
        db.Database.SqlQueryRaw<long>("SELECT rowid AS Value FROM offers WHERE id = {0}", id).SingleAsync();

    private static Task<List<long>> Matches(JobRadarDbContext db, string word) =>
        db.Database.SqlQueryRaw<long>("SELECT rowid AS Value FROM offers_fts WHERE offers_fts MATCH {0}", word).ToListAsync();

    private static async Task<List<string>> ObjectNames(string path, string type)
    {
        await using var db = Open(path);
        return await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = {0}", type).ToListAsync();
    }

    private static async Task<List<string>> OffersIndexNames(string path)
    {
        await using var db = Open(path);
        return await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'index' AND tbl_name = 'offers'").ToListAsync();
    }

    private static async Task<List<string>> IndexColumns(string path, string index)
    {
        await using var db = Open(path);
        return await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_index_info({0})", index).ToListAsync();
    }

    private static async Task<List<string>> SchemaSnapshot(string path)
    {
        await using var db = Open(path);
        return await db.Database.SqlQueryRaw<string>(
            "SELECT type || ':' || name || ':' || IFNULL(sql, '') AS Value FROM sqlite_master ORDER BY type, name").ToListAsync();
    }

    private static async Task<long> AppliedMigrationCount(string path)
    {
        await using var db = Open(path);
        return await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM \"__EFMigrationsHistory\"").SingleAsync();
    }
}
