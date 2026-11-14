using System.Globalization;
using JobRadar.Core.Identity;
using JobRadar.Core.Models;
using JobRadar.Core.Ports;
using JobRadar.Store;
using JobRadar.Store.Model;
using JobRadar.Store.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Store.Tests;

/// <summary>The offer upsert on a real SQLite file (never the InMemory provider).</summary>
public sealed class OfferUpsertTests : IDisposable
{
    private static readonly DateOnly Day1 = new(2026, 11, 13);
    private static readonly DateOnly Day2 = new(2026, 11, 14);

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
    public async Task First_sighting_inserts_a_new_offer()
    {
        var path = await MigratedPathAsync();
        var offer = Synthetic("1001");

        var result = await UpsertWithFreshContextAsync(path, [offer], Day1);

        Assert.Equal(1, result.New);
        Assert.Equal(0, result.Changed);
        Assert.Equal(0, result.Unchanged);
        Assert.Equal([OfferId.Of("greenhouse", "1001")], result.NewIds);
        Assert.Empty(result.ChangedIds);

        var row = await ReadRowAsync(path, OfferId.Of("greenhouse", "1001"));
        Assert.Equal(OfferId.Of("greenhouse", "1001"), row.Id);
        Assert.Equal("greenhouse", row.Source);
        Assert.Equal("1001", row.SourceId);
        Assert.Equal("https://example.org/jobs/1001", row.Url);
        Assert.Equal(CanonicalKey.Of("Acme", "Platform Engineer", "PL"), row.CanonicalKey);
        Assert.Null(row.PrimaryOfferId);
        Assert.Equal("Platform Engineer", row.Title);
        Assert.Equal("Acme", row.Company);
        Assert.Equal("PL", row.Country);
        Assert.Equal("Warsaw", row.Location);
        Assert.Equal("hybrid", row.WorkMode);
        Assert.Equal(2, row.OfficeDaysPerWeek);
        Assert.Equal("B2B", row.Contract);
        Assert.Equal(20000, row.SalaryMin);
        Assert.Equal(26000, row.SalaryMax);
        Assert.Equal("PLN", row.Currency);
        Assert.Equal("month", row.SalaryPeriod);
        Assert.Equal("Synthetic posting text.", row.Description);
        Assert.Equal(DescriptionHash.Of("Synthetic posting text."), row.DescriptionHash);
        Assert.Equal("2026-11-10T08:00:00.0000000+00:00", row.PostedAt);
        Assert.Equal("2026-11-13", row.FirstSeenAt);
        Assert.Equal("2026-11-13", row.LastSeenAt);
        Assert.Equal("new", row.Lifecycle);
        Assert.Equal(0, row.MissedRuns);
        Assert.Equal("unknown", row.Liveness);
        Assert.Null(row.LastCheckedAt);
        Assert.Null(row.ExpiredAt);
        Assert.Null(row.PrefilterReason);
        Assert.False(row.NeedsReview);
    }

    [Fact]
    public async Task Repeat_updates_only_last_seen()
    {
        var path = await MigratedPathAsync();
        var offer = Synthetic("1001");
        await UpsertWithFreshContextAsync(path, [offer], Day1);

        // Simulate one missed run so that the reset of missed_runs is observable.
        await using (var db = Open(path))
        {
            var row = await db.Offers.SingleAsync(o => o.Id == OfferId.Of("greenhouse", "1001"));
            row.MissedRuns = 2;
            await db.SaveChangesAsync();
        }

        var before = Columns(await ReadRowAsync(path, OfferId.Of("greenhouse", "1001")));
        var result = await UpsertWithFreshContextAsync(path, [offer], Day2);
        var after = Columns(await ReadRowAsync(path, OfferId.Of("greenhouse", "1001")));

        Assert.Equal(0, result.New);
        Assert.Equal(0, result.Changed);
        Assert.Equal(1, result.Unchanged);

        var differing = before.Keys.Where(k => before[k] != after[k]).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(["last_seen_at", "missed_runs"], differing);
        Assert.Equal("2026-11-14", after["last_seen_at"]);
        Assert.Equal("0", after["missed_runs"]);
    }

    [Fact]
    public async Task Repeat_after_expiry_reactivates()
    {
        var path = await MigratedPathAsync();
        var offer = Synthetic("1001");
        await UpsertWithFreshContextAsync(path, [offer], Day1);

        await using (var db = Open(path))
        {
            var row = await db.Offers.SingleAsync(o => o.Id == OfferId.Of("greenhouse", "1001"));
            row.Lifecycle = "expired";
            row.ExpiredAt = "2026-11-12";
            row.MissedRuns = 3;
            await db.SaveChangesAsync();
        }

        var result = await UpsertWithFreshContextAsync(path, [offer], Day2);

        Assert.Equal(0, result.New);
        Assert.Equal(0, result.Changed);
        Assert.Equal(1, result.Unchanged);

        var row2 = await ReadRowAsync(path, OfferId.Of("greenhouse", "1001"));
        Assert.Equal("active", row2.Lifecycle);
        Assert.Null(row2.ExpiredAt);
        Assert.Equal(0, row2.MissedRuns);
        Assert.Equal("2026-11-14", row2.LastSeenAt);
    }

    [Fact]
    public async Task Changed_description_is_reported_changed()
    {
        var path = await MigratedPathAsync();
        await UpsertWithFreshContextAsync(path, [Synthetic("1001", description: "Original text.")], Day1);

        var result = await UpsertWithFreshContextAsync(path, [Synthetic("1001", description: "Updated text.")], Day2);

        Assert.Equal(0, result.New);
        Assert.Equal(1, result.Changed);
        Assert.Equal(0, result.Unchanged);
        Assert.Empty(result.NewIds);
        Assert.Equal([OfferId.Of("greenhouse", "1001")], result.ChangedIds);

        var row = await ReadRowAsync(path, OfferId.Of("greenhouse", "1001"));
        Assert.Equal("Updated text.", row.Description);
        Assert.Equal(DescriptionHash.Of("Updated text."), row.DescriptionHash);
        Assert.Equal("new", row.Lifecycle);
        Assert.Equal("2026-11-14", row.LastSeenAt);
    }

    [Fact]
    public async Task Same_batch_twice_gives_identical_rows()
    {
        var path = await MigratedPathAsync();
        var batch = new[] { Synthetic("1001"), Synthetic("1002", title: "Data Engineer"), Synthetic("1003", title: "SRE") };

        await UpsertWithFreshContextAsync(path, batch, Day1);
        var afterFirst = await AllRowsAsync(path);

        var second = await UpsertWithFreshContextAsync(path, batch, Day1);
        var afterSecond = await AllRowsAsync(path);

        Assert.Equal(0, second.New);
        Assert.Equal(0, second.Changed);
        Assert.Equal(3, second.Unchanged);
        Assert.Equal(3, afterSecond.Count);
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public async Task Duplicates_inside_one_batch_keep_the_last()
    {
        var path = await MigratedPathAsync();
        var batch = new[]
        {
            Synthetic("1001", title: "First version"),
            Synthetic("1002"),
            Synthetic("1001", title: "Last version"),
        };

        var result = await UpsertWithFreshContextAsync(path, batch, Day1);

        Assert.Equal(2, result.New);
        Assert.Equal(2, result.NewIds.Count);
        await using var db = Open(path);
        Assert.Equal(2, await db.Offers.CountAsync());
        var row = await db.Offers.AsNoTracking().SingleAsync(o => o.Id == OfferId.Of("greenhouse", "1001"));
        Assert.Equal("Last version", row.Title);
    }

    [Fact]
    public async Task Archived_offer_description_is_not_replaced()
    {
        var path = await MigratedPathAsync();
        await UpsertWithFreshContextAsync(path, [Synthetic("1001", description: "Original text.")], Day1);

        await using (var db = Open(path))
        {
            var row = await db.Offers.SingleAsync(o => o.Id == OfferId.Of("greenhouse", "1001"));
            row.Lifecycle = "archived";
            await db.SaveChangesAsync();
        }

        var result = await UpsertWithFreshContextAsync(path, [Synthetic("1001", description: "Replacement text.")], Day2);

        Assert.Equal(0, result.Changed);
        Assert.Equal(1, result.Unchanged);
        Assert.Empty(result.ChangedIds);

        var row2 = await ReadRowAsync(path, OfferId.Of("greenhouse", "1001"));
        Assert.Equal("Original text.", row2.Description);
        Assert.Equal(DescriptionHash.Of("Original text."), row2.DescriptionHash);
        Assert.Equal("archived", row2.Lifecycle);
        Assert.Equal("2026-11-14", row2.LastSeenAt);
    }

    [Fact]
    public async Task A_failing_row_rolls_back_the_batch()
    {
        var path = await MigratedPathAsync();
        var batch = new[]
        {
            Synthetic("1001"),
            Synthetic("1002"),
            Synthetic("1003") with { Url = null! },
        };

        await using (var db = Open(path))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => new OfferUpsert(db).UpsertAsync(batch, Day1, default));
        }

        await using var check = Open(path);
        Assert.Equal(0, await check.Offers.CountAsync());
    }

    /// <summary>A synthetic posting from the fictional source "greenhouse".</summary>
    private static RawOffer Synthetic(string sourceId, string title = "Platform Engineer", string description = "Synthetic posting text.") =>
        new(
            Source: "greenhouse",
            SourceId: sourceId,
            Url: "https://example.org/jobs/" + sourceId,
            Title: title,
            Company: "Acme",
            Country: "PL",
            Location: "Warsaw",
            WorkMode: WorkMode.Hybrid,
            OfficeDaysPerWeek: 2,
            Contract: "B2B",
            SalaryMin: 20000,
            SalaryMax: 26000,
            Currency: "PLN",
            SalaryPeriod: SalaryPeriod.Month,
            Description: description,
            PostedAt: new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero),
            Attribution: null);

    private static async Task<UpsertResult> UpsertWithFreshContextAsync(string path, IReadOnlyList<RawOffer> batch, DateOnly runDate)
    {
        await using var db = Open(path);
        return await new OfferUpsert(db).UpsertAsync(batch, runDate, default);
    }

    private static async Task<OfferRow> ReadRowAsync(string path, string id)
    {
        await using var db = Open(path);
        return await db.Offers.AsNoTracking().SingleAsync(o => o.Id == id);
    }

    /// <summary>Every row as one text line of <c>column=value</c> pairs, so that equality compares values, not references.</summary>
    private static async Task<List<string>> AllRowsAsync(string path)
    {
        await using var db = Open(path);
        var rows = await db.Offers.AsNoTracking().OrderBy(o => o.Id).ToListAsync();
        return rows
            .Select(row => string.Join(";", Columns(row).OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => c.Key + "=" + c.Value)))
            .ToList();
    }

    /// <summary>Every column of a row as text, keyed by column name. Reflection keeps the comparison complete.</summary>
    private static Dictionary<string, string?> Columns(OfferRow row)
    {
        var columns = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var property in typeof(OfferRow).GetProperties())
        {
            var value = property.GetValue(row);
            columns[ToColumnName(property.Name)] = value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return columns;
    }

    private static string ToColumnName(string propertyName) =>
        string.Concat(propertyName.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    private static JobRadarDbContext Open(string path) =>
        new(new DbContextOptionsBuilder<JobRadarDbContext>().UseSqlite("Data Source=" + path).Options);
}
