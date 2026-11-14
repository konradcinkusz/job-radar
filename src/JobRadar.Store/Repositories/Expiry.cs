using System.Globalization;
using System.Text.Json;
using JobRadar.Core.Machine;
using JobRadar.Core.Models;
using JobRadar.Store.Model;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Store.Repositories;

/// <summary>The result of one <see cref="Expiry.RecordMissedAsync"/> call: offers whose <c>missed_runs</c> grew, and those that expired.</summary>
public sealed record MissedResult(int Missed, int Expired);

/// <summary>
/// The lifecycle transitions that no sighting triggers: missed runs, liveness death, and archiving (SPEC §6.4, SPEC §5 R8).
/// The conditions of SPEC (source healthy, <c>expiry_by_absence</c>, <c>missed_runs</c>, expiry age) are checked here;
/// <see cref="LifecycleMachine"/> checks that each pair of state and event is allowed.
/// </summary>
public sealed class Expiry(JobRadarDbContext db)
{
    private const string DateFormat = "yyyy-MM-dd";
    private const int MissedRunsToExpire = 3;

    /// <summary>
    /// Counts one run in which the source was absent. Runs only when the source is healthy and its listing is complete
    /// (<c>expiry_by_absence</c>); otherwise it changes nothing. Offers whose <c>source_id</c> is in <paramref name="seenSourceIds"/>
    /// are not touched here: a repeat sighting resets their counter (<see cref="OfferUpsert"/>).
    /// </summary>
    public async Task<MissedResult> RecordMissedAsync(
        string source,
        IReadOnlySet<string> seenSourceIds,
        bool sourceHealthy,
        bool expiryByAbsence,
        DateOnly runDate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(seenSourceIds);

        if (!(sourceHealthy && expiryByAbsence))
        {
            return new MissedResult(0, 0);
        }

        var runDateText = runDate.ToString(DateFormat, CultureInfo.InvariantCulture);
        var openStates = new[] { ToSnake(Lifecycle.New), ToSnake(Lifecycle.Active) };
        var candidates = await db.Offers
            .Where(o => o.Source == source && openStates.Contains(o.Lifecycle))
            .ToListAsync(ct);

        var missed = 0;
        var expired = 0;
        foreach (var row in candidates.Where(o => !seenSourceIds.Contains(o.SourceId)))
        {
            var state = ParseLifecycle(row.Lifecycle);
            // Missed leaves the state unchanged for new and active; the check keeps the table of SPEC §6.4 in one place.
            row.Lifecycle = ToSnake(Next(state, LifecycleEvent.Missed, row.Id));
            row.MissedRuns += 1;
            missed++;

            if (row.MissedRuns >= MissedRunsToExpire)
            {
                row.Lifecycle = ToSnake(Next(state, LifecycleEvent.MissedThrice, row.Id));
                row.ExpiredAt = runDateText;
                expired++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new MissedResult(missed, expired);
    }

    /// <summary>
    /// Expires one offer that the liveness check found dead (SPEC §6.4, event <c>Dead</c>). Returns false, and changes nothing,
    /// for an unknown id or for a state the machine does not allow to die (already expired or archived).
    /// </summary>
    public async Task<bool> MarkDeadAsync(string offerId, DateOnly runDate, CancellationToken ct = default)
    {
        var row = await db.Offers.FirstOrDefaultAsync(o => o.Id == offerId, ct);
        if (row is null)
        {
            return false;
        }

        var state = ParseLifecycle(row.Lifecycle);
        if (LifecycleMachine.Next(state, LifecycleEvent.Dead) is not LifecycleResult.Ok { Next: Lifecycle.Expired })
        {
            return false;
        }

        row.Lifecycle = ToSnake(Lifecycle.Expired);
        row.ExpiredAt = runDate.ToString(DateFormat, CultureInfo.InvariantCulture);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Archives every expired offer whose <c>expired_at</c> is strictly more than <paramref name="afterDays"/> days before
    /// <paramref name="runDate"/> (SPEC §6.4, event <c>Aged</c>). The description is purged; <c>description_hash</c> and the
    /// offer fields stay. Returns the number of offers archived.
    /// </summary>
    public async Task<int> ArchiveAsync(DateOnly runDate, int afterDays, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterDays);

        var expiredState = ToSnake(Lifecycle.Expired);
        var expiredRows = await db.Offers
            .Where(o => o.Lifecycle == expiredState && o.ExpiredAt != null)
            .ToListAsync(ct);

        var archived = 0;
        foreach (var row in expiredRows)
        {
            var expiredAt = DateOnly.Parse(row.ExpiredAt!, CultureInfo.InvariantCulture);
            if (runDate.DayNumber - expiredAt.DayNumber <= afterDays)
            {
                continue;
            }

            row.Lifecycle = ToSnake(Next(Lifecycle.Expired, LifecycleEvent.Aged, row.Id));
            row.Description = string.Empty;
            archived++;
        }

        await db.SaveChangesAsync(ct);
        return archived;
    }

    private static Lifecycle Next(Lifecycle state, LifecycleEvent e, string offerId)
    {
        var result = LifecycleMachine.Next(state, e);
        if (result is LifecycleResult.Invalid invalid)
        {
            throw new InvalidOperationException($"offer {offerId}: {invalid.Reason}");
        }

        return ((LifecycleResult.Ok)result).Next;
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
