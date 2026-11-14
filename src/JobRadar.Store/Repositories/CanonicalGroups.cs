using JobRadar.Core.Ports;
using JobRadar.Store.Model;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Store.Repositories;

/// <summary>
/// Chooses the primary of each canonical group and points the other members at it (SPEC §6.4 "Duplicates").
/// The primary is the member with the highest <c>SourceKind</c>; ties go to the earliest <c>first_seen_at</c>,
/// then to the lexicographically smaller <c>id</c>. One call is one transaction.
/// </summary>
public sealed class CanonicalGroups(JobRadarDbContext db, ISourceKinds kinds)
{
    /// <summary>Recomputes <c>primary_offer_id</c> for every group of the given keys. Returns the number of rows whose value changed.</summary>
    public async Task<int> RecomputeAsync(IReadOnlyCollection<string> canonicalKeys, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(canonicalKeys);
        if (canonicalKeys.Count == 0)
        {
            return 0;
        }

        var changed = 0;
        var keys = canonicalKeys.Distinct(StringComparer.Ordinal).ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        foreach (var key in keys)
        {
            var members = await db.Offers.Where(o => o.CanonicalKey == key).ToListAsync(ct);
            if (members.Count == 0)
            {
                continue;
            }

            var primary = Choose(members);
            foreach (var member in members)
            {
                var wanted = member.Id == primary.Id ? null : primary.Id;
                if (member.PrimaryOfferId != wanted)
                {
                    member.PrimaryOfferId = wanted;
                    changed++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return changed;
    }

    private OfferRow Choose(IReadOnlyList<OfferRow> members) =>
        members
            .OrderByDescending(o => (int)kinds.KindOf(o.Source))
            .ThenBy(o => o.FirstSeenAt, StringComparer.Ordinal)
            .ThenBy(o => o.Id, StringComparer.Ordinal)
            .First();
}
