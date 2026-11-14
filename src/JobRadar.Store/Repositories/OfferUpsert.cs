using System.Globalization;
using System.Text.Json;
using JobRadar.Core.Identity;
using JobRadar.Core.Machine;
using JobRadar.Core.Models;
using JobRadar.Core.Ports;
using JobRadar.Store.Model;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Store.Repositories;

/// <summary>
/// The writer of <c>offers</c> for one scrape batch (SPEC §6.4 "Duplicates", lifecycle event <c>Seen</c>).
/// One call is one transaction: either every offer of the batch is persisted or none is.
/// </summary>
public sealed class OfferUpsert(JobRadarDbContext db) : IOfferStore
{
    private const string DateFormat = "yyyy-MM-dd";

    public async Task<UpsertResult> UpsertAsync(IReadOnlyList<RawOffer> offers, DateOnly runDate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(offers);

        var batch = CollapseDuplicates(offers);
        var runDateText = runDate.ToString(DateFormat, CultureInfo.InvariantCulture);
        var newIds = new List<string>();
        var changedIds = new List<string>();
        var unchanged = 0;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ids = batch.Select(item => item.Id).ToList();
        var existing = await db.Offers.Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, ct);

        foreach (var (id, offer) in batch)
        {
            if (existing.TryGetValue(id, out var row))
            {
                if (ApplyRepeat(row, offer, runDateText))
                {
                    changedIds.Add(id);
                }
                else
                {
                    unchanged++;
                }
            }
            else
            {
                db.Offers.Add(NewRow(id, offer, runDateText));
                newIds.Add(id);
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new UpsertResult(newIds.Count, changedIds.Count, unchanged, newIds, changedIds);
    }

    /// <summary>Keeps one entry per (source, source_id): the last occurrence, at its position in the batch.</summary>
    private static List<(string Id, RawOffer Offer)> CollapseDuplicates(IReadOnlyList<RawOffer> offers)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<(string Id, RawOffer Offer)>();

        for (var i = offers.Count - 1; i >= 0; i--)
        {
            var offer = offers[i];
            var id = OfferId.Of(offer.Source, offer.SourceId);
            if (seen.Add(id))
            {
                kept.Add((id, offer));
            }
        }

        kept.Reverse();
        return kept;
    }

    private static OfferRow NewRow(string id, RawOffer offer, string runDateText) => new()
    {
        Id = id,
        Source = offer.Source,
        SourceId = offer.SourceId,
        Url = offer.Url,
        CanonicalKey = CanonicalKey.Of(offer.Company, offer.Title, offer.Country),
        PrimaryOfferId = null,
        Title = offer.Title,
        Company = offer.Company,
        Country = offer.Country,
        Location = offer.Location,
        WorkMode = ToSnake(offer.WorkMode),
        OfficeDaysPerWeek = offer.OfficeDaysPerWeek,
        Contract = offer.Contract,
        SalaryMin = offer.SalaryMin,
        SalaryMax = offer.SalaryMax,
        Currency = offer.Currency,
        SalaryPeriod = offer.SalaryPeriod is { } period ? ToSnake(period) : null,
        Description = offer.Description,
        DescriptionHash = DescriptionHash.Of(offer.Description),
        PostedAt = offer.PostedAt?.ToString("o", CultureInfo.InvariantCulture),
        FirstSeenAt = runDateText,
        LastSeenAt = runDateText,
        Lifecycle = ToSnake(Lifecycle.New),
        MissedRuns = 0,
        Liveness = ToSnake(Liveness.Unknown),
        LastCheckedAt = null,
        ExpiredAt = null,
        PrefilterReason = null,
        NeedsReview = false,
    };

    /// <summary>Applies a repeat sighting to an existing row. Returns true when the description was replaced.</summary>
    private static bool ApplyRepeat(OfferRow row, RawOffer offer, string runDateText)
    {
        var current = ParseLifecycle(row.Lifecycle);
        var result = LifecycleMachine.Next(current, LifecycleEvent.Seen);
        if (result is LifecycleResult.Invalid invalid)
        {
            throw new InvalidOperationException($"offer {row.Id}: {invalid.Reason}");
        }

        var next = ((LifecycleResult.Ok)result).Next;
        if (current == Lifecycle.Expired && next == Lifecycle.Active)
        {
            row.ExpiredAt = null;
        }

        row.Lifecycle = ToSnake(next);
        row.LastSeenAt = runDateText;
        row.MissedRuns = 0;

        // An archived offer had its description purged (SPEC §6.4): it is never refilled.
        var newHash = DescriptionHash.Of(offer.Description);
        if (current == Lifecycle.Archived || newHash == row.DescriptionHash)
        {
            return false;
        }

        row.Description = offer.Description;
        row.DescriptionHash = newHash;
        return true;
    }

    private static string ToSnake<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    private static Lifecycle ParseLifecycle(string stored)
    {
        foreach (var value in Enum.GetValues<Lifecycle>())
        {
            if (ToSnake(value) == stored)
            {
                return value;
            }
        }

        throw new InvalidOperationException($"unknown lifecycle '{stored}' in an offers row");
    }
}
