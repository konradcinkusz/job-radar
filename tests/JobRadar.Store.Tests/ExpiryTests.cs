using JobRadar.Core.Identity;
using JobRadar.Store;
using JobRadar.Store.Model;
using JobRadar.Store.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Store.Tests;

/// <summary>Missed runs, liveness death and archiving on a real SQLite file (never the InMemory provider).</summary>
public sealed class ExpiryTests : IDisposable
{
    private static readonly DateOnly Day1 = new(2026, 11, 13);
    private static readonly DateOnly Day2 = new(2026, 11, 14);
    private static readonly DateOnly Day3 = new(2026, 11, 15);
    private static readonly DateOnly Today = new(2026, 11, 14);
    private static readonly IReadOnlySet<string> NobodySeen = new HashSet<string>(StringComparer.Ordinal);

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
    public async Task Healthy_source_missing_three_times_expires_the_offer()
    {
        var path = await MigratedPathAsync();
        await InsertAsync(path,
            Offer("1001", "new"),
            Offer("1004", "active"),
            Offer("2001", "new", source: "lever"),
            Offer("1003", "expired", missedRuns: 3, expiredAt: "2026-11-01"));

        var first = await MissedAsync(path, "greenhouse", healthy: true, expiryByAbsence: true, Day1);
        Assert.Equal(new MissedResult(2, 0), first);
        var afterFirst = await ReadAsync(path, "1001");
        Assert.Equal("new", afterFirst.Lifecycle);
        Assert.Equal(1, afterFirst.MissedRuns);
        Assert.Null(afterFirst.ExpiredAt);

        var second = await MissedAsync(path, "greenhouse", healthy: true, expiryByAbsence: true, Day2);
        Assert.Equal(new MissedResult(2, 0), second);
        var afterSecond = await ReadAsync(path, "1001");
        Assert.Equal("new", afterSecond.Lifecycle);
        Assert.Equal(2, afterSecond.MissedRuns);
        Assert.Null(afterSecond.ExpiredAt);

        var third = await MissedAsync(path, "greenhouse", healthy: true, expiryByAbsence: true, Day3);
        Assert.Equal(new MissedResult(2, 2), third);
        var afterThird = await ReadAsync(path, "1001");
        Assert.Equal("expired", afterThird.Lifecycle);
        Assert.Equal(3, afterThird.MissedRuns);
        Assert.Equal("2026-11-15", afterThird.ExpiredAt);

        // An active offer follows the same rule.
        Assert.Equal("expired", (await ReadAsync(path, "1004")).Lifecycle);

        // Another source and an offer that is already expired do not change.
        var lever = await ReadAsync(path, "2001", source: "lever");
        Assert.Equal("new", lever.Lifecycle);
        Assert.Equal(0, lever.MissedRuns);
        var alreadyExpired = await ReadAsync(path, "1003");
        Assert.Equal("expired", alreadyExpired.Lifecycle);
        Assert.Equal(3, alreadyExpired.MissedRuns);
        Assert.Equal("2026-11-01", alreadyExpired.ExpiredAt);
    }

    [Fact]
    public async Task Unhealthy_source_never_expires_offers()
    {
        var path = await MigratedPathAsync();
        await InsertAsync(path, Offer("1001", "active"));

        for (var day = 0; day < 5; day++)
        {
            var result = await MissedAsync(path, "greenhouse", healthy: false, expiryByAbsence: true, Day1.AddDays(day));
            Assert.Equal(new MissedResult(0, 0), result);
        }

        var row = await ReadAsync(path, "1001");
        Assert.Equal("active", row.Lifecycle);
        Assert.Equal(0, row.MissedRuns);
        Assert.Null(row.ExpiredAt);
    }

    [Fact]
    public async Task Source_without_expiry_by_absence_never_expires_offers()
    {
        var path = await MigratedPathAsync();
        await InsertAsync(path, Offer("1001", "active"));

        for (var day = 0; day < 5; day++)
        {
            var result = await MissedAsync(path, "greenhouse", healthy: true, expiryByAbsence: false, Day1.AddDays(day));
            Assert.Equal(new MissedResult(0, 0), result);
        }

        var row = await ReadAsync(path, "1001");
        Assert.Equal("active", row.Lifecycle);
        Assert.Equal(0, row.MissedRuns);
        Assert.Null(row.ExpiredAt);
    }

    [Fact]
    public async Task Seen_again_resets_missed_runs()
    {
        var path = await MigratedPathAsync();
        await InsertAsync(path,
            Offer("1001", "active", missedRuns: 2),
            Offer("1002", "new", missedRuns: 2));

        // 1001 is in the seen set: RecordMissedAsync leaves it alone. OfferUpsert resets its counter on a repeat sighting
        // (OfferUpsertTests.Repeat_updates_only_last_seen); this test does not do that reset, so the counter stays at 2.
        var seen = new HashSet<string>(["1001"], StringComparer.Ordinal);
        var result = await MissedAsync(path, "greenhouse", healthy: true, expiryByAbsence: true, Day3, seen);

        Assert.Equal(new MissedResult(1, 1), result);
        var seenRow = await ReadAsync(path, "1001");
        Assert.Equal("active", seenRow.Lifecycle);
        Assert.Equal(2, seenRow.MissedRuns);
        Assert.Null(seenRow.ExpiredAt);

        // 1002 is not seen: its counter reaches 3 and it expires.
        var missingRow = await ReadAsync(path, "1002");
        Assert.Equal("expired", missingRow.Lifecycle);
        Assert.Equal(3, missingRow.MissedRuns);
        Assert.Equal("2026-11-15", missingRow.ExpiredAt);
    }

    [Fact]
    public async Task MarkDead_expires_a_new_or_active_offer_and_ignores_others()
    {
        var path = await MigratedPathAsync();
        await InsertAsync(path,
            Offer("1001", "new"),
            Offer("1002", "active"),
            Offer("1003", "expired", expiredAt: "2026-11-01"),
            Offer("1004", "archived", expiredAt: "2026-09-01", description: string.Empty));

        Assert.True(await MarkDeadAsync(path, OfferId.Of("greenhouse", "1001"), Today));
        Assert.True(await MarkDeadAsync(path, OfferId.Of("greenhouse", "1002"), Today));
        Assert.False(await MarkDeadAsync(path, OfferId.Of("greenhouse", "1003"), Today));
        Assert.False(await MarkDeadAsync(path, OfferId.Of("greenhouse", "1004"), Today));
        Assert.False(await MarkDeadAsync(path, OfferId.Of("greenhouse", "9999"), Today));

        var newRow = await ReadAsync(path, "1001");
        Assert.Equal("expired", newRow.Lifecycle);
        Assert.Equal("2026-11-14", newRow.ExpiredAt);
        Assert.Equal("expired", (await ReadAsync(path, "1002")).Lifecycle);

        var expiredRow = await ReadAsync(path, "1003");
        Assert.Equal("expired", expiredRow.Lifecycle);
        Assert.Equal("2026-11-01", expiredRow.ExpiredAt);

        var archivedRow = await ReadAsync(path, "1004");
        Assert.Equal("archived", archivedRow.Lifecycle);
        Assert.Equal("2026-09-01", archivedRow.ExpiredAt);
    }

    [Fact]
    public async Task Archive_purges_the_description()
    {
        var path = await MigratedPathAsync();
        var description = "Synthetic posting text.";
        await InsertAsync(path, Offer("1001", "expired", expiredAt: "2026-10-05", description: description));
        var hashBefore = (await ReadAsync(path, "1001")).DescriptionHash;

        var archived = await ArchiveAsync(path, Today, afterDays: 30);

        Assert.Equal(1, archived);
        var row = await ReadAsync(path, "1001");
        Assert.Equal("archived", row.Lifecycle);
        Assert.Equal(string.Empty, row.Description);
        Assert.Equal(hashBefore, row.DescriptionHash);
        Assert.Equal(DescriptionHash.Of(description), row.DescriptionHash);
        Assert.Equal("2026-10-05", row.ExpiredAt);
    }

    [Fact]
    public async Task Archive_waits_for_the_age()
    {
        var path = await MigratedPathAsync();
        await InsertAsync(path,
            Offer("1001", "expired", expiredAt: "2026-10-15"), // 30 days before the run: not yet older than 30
            Offer("1002", "expired", expiredAt: "2026-10-14")); // 31 days before the run: older than 30

        var archived = await ArchiveAsync(path, Today, afterDays: 30);

        Assert.Equal(1, archived);
        var notYet = await ReadAsync(path, "1001");
        Assert.Equal("expired", notYet.Lifecycle);
        Assert.Equal("Synthetic posting text.", notYet.Description);

        var aged = await ReadAsync(path, "1002");
        Assert.Equal("archived", aged.Lifecycle);
        Assert.Equal(string.Empty, aged.Description);
    }

    private static OfferRow Offer(
        string sourceId,
        string lifecycle,
        string source = "greenhouse",
        int missedRuns = 0,
        string? expiredAt = null,
        string description = "Synthetic posting text.") => new()
    {
        Id = OfferId.Of(source, sourceId),
        Source = source,
        SourceId = sourceId,
        Url = "https://example.org/jobs/" + sourceId,
        CanonicalKey = CanonicalKey.Of("Acme", "Platform Engineer", "PL"),
        Title = "Platform Engineer",
        Company = "Acme",
        WorkMode = "hybrid",
        Description = description,
        DescriptionHash = DescriptionHash.Of(description),
        FirstSeenAt = "2026-11-01",
        LastSeenAt = "2026-11-01",
        Lifecycle = lifecycle,
        MissedRuns = missedRuns,
        Liveness = "unknown",
        ExpiredAt = expiredAt,
    };

    private static async Task InsertAsync(string path, params OfferRow[] rows)
    {
        await using var db = Open(path);
        db.Offers.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private static async Task<MissedResult> MissedAsync(
        string path,
        string source,
        bool healthy,
        bool expiryByAbsence,
        DateOnly runDate,
        IReadOnlySet<string>? seen = null)
    {
        await using var db = Open(path);
        return await new Expiry(db).RecordMissedAsync(source, seen ?? NobodySeen, healthy, expiryByAbsence, runDate);
    }

    private static async Task<bool> MarkDeadAsync(string path, string offerId, DateOnly runDate)
    {
        await using var db = Open(path);
        return await new Expiry(db).MarkDeadAsync(offerId, runDate);
    }

    private static async Task<int> ArchiveAsync(string path, DateOnly runDate, int afterDays)
    {
        await using var db = Open(path);
        return await new Expiry(db).ArchiveAsync(runDate, afterDays);
    }

    private static async Task<OfferRow> ReadAsync(string path, string sourceId, string source = "greenhouse")
    {
        var id = OfferId.Of(source, sourceId);
        await using var db = Open(path);
        return await db.Offers.AsNoTracking().SingleAsync(o => o.Id == id);
    }

    private static JobRadarDbContext Open(string path) =>
        new(new DbContextOptionsBuilder<JobRadarDbContext>().UseSqlite("Data Source=" + path).Options);
}
