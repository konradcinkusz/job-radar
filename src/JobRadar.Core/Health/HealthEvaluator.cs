namespace JobRadar.Core.Health;

/// <summary>Verdict of the health check of one source in one run. <c>Reason</c> is null when healthy.</summary>
public sealed record HealthVerdict(bool Healthy, string? Reason);

/// <summary>
/// Health of a source in one run (SPEC §6.3 Health). Pure: BCL only, no I/O, no clock.
/// <c>previousHealthyCounts</c> is ordered newest first; only the first seven values are used.
/// </summary>
public static class HealthEvaluator
{
    private const int HistoryWindow = 7;
    private const int MinimumHistory = 3;

    /// <summary>
    /// Unhealthy when today's count is zero, or when there are at least three previous healthy counts and
    /// <paramref name="todayCount"/> is strictly below 20 % of the median of the last seven of them.
    /// Errors change the verdict only by a suffix on the zero-offers reason.
    /// </summary>
    public static HealthVerdict Evaluate(int todayCount, IReadOnlyList<int> previousHealthyCounts, IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(previousHealthyCounts);
        ArgumentNullException.ThrowIfNull(errors);

        if (todayCount == 0)
        {
            string reason = "0 offers found today";
            if (errors.Count > 0)
            {
                reason += FormattableString.Invariant($" ({errors.Count} error(s))");
            }

            return new HealthVerdict(false, reason);
        }

        if (previousHealthyCounts.Count >= MinimumHistory)
        {
            int[] used = previousHealthyCounts.Take(HistoryWindow).ToArray();
            double median = Median(used);

            // todayCount < 0.2 × median, written without the 0.2 factor to avoid rounding at the boundary.
            if (todayCount * 5.0 < median)
            {
                return new HealthVerdict(false, FormattableString.Invariant(
                    $"{todayCount} offers found today, below 20% of the median {median:0.#} of the last {used.Length} healthy runs"));
            }
        }

        return new HealthVerdict(true, null);
    }

    /// <summary>Median of a non-empty sequence: the middle value, or the mean of the two middle values for an even count.</summary>
    private static double Median(int[] values)
    {
        int[] sorted = values.OrderBy(v => v).ToArray();
        int n = sorted.Length;
        return n % 2 == 1
            ? sorted[n / 2]
            : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }
}
