using System.Text.Json;
using JobRadar.Store.Model;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Store.Repositories;

/// <summary>One run of one source and kind, as written to <c>runs</c> (SPEC §6.4). Timestamps are ISO 8601 UTC text.</summary>
public sealed record RunRecord(
    string RunId,
    string Source,
    string Kind,
    string StartedAt,
    string FinishedAt,
    int OffersFound,
    int OffersNew,
    int OffersChanged,
    IReadOnlyList<string> Errors,
    bool Healthy);

/// <summary>The writer and reader of <c>runs</c> (SPEC §6.4, SPEC §5 R8). The key is <c>(run_id, source, kind)</c>.</summary>
public sealed class Runs(JobRadarDbContext db)
{
    private const string ScrapeKind = "scrape";

    /// <summary>
    /// Writes one run. A row with the same <c>(run_id, source, kind)</c> is replaced, so recording the same run twice
    /// leaves one row with the latest values.
    /// </summary>
    public async Task RecordAsync(RunRecord run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var row = await db.Runs.FindAsync([run.RunId, run.Source, run.Kind], ct);
        if (row is null)
        {
            row = new RunRow
            {
                RunId = run.RunId,
                Source = run.Source,
                Kind = run.Kind,
            };
            db.Runs.Add(row);
        }

        row.StartedAt = run.StartedAt;
        row.FinishedAt = run.FinishedAt;
        row.OffersFound = run.OffersFound;
        row.OffersNew = run.OffersNew;
        row.OffersChanged = run.OffersChanged;
        row.Errors = JsonSerializer.Serialize(run.Errors);
        row.Healthy = run.Healthy;

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The <c>offers_found</c> of the last <paramref name="take"/> healthy scrape runs of <paramref name="source"/>, newest first.
    /// Unhealthy runs and runs of other kinds or sources are skipped.
    /// </summary>
    public async Task<IReadOnlyList<int>> LastHealthyCountsAsync(string source, int take, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(take);

        return await db.Runs
            .AsNoTracking()
            .Where(r => r.Source == source && r.Kind == ScrapeKind && r.Healthy)
            .OrderByDescending(r => r.StartedAt)
            .ThenByDescending(r => r.RunId)
            .Select(r => r.OffersFound)
            .Take(take)
            .ToListAsync(ct);
    }
}
