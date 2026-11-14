using JobRadar.Core.Health;

namespace JobRadar.Core.Tests;

public class HealthEvaluatorTests
{
    private static readonly string[] NoErrors = [];

    [Fact]
    public void Zero_offers_is_unhealthy()
    {
        var verdict = HealthEvaluator.Evaluate(0, [100, 100, 100], NoErrors);

        Assert.False(verdict.Healthy);
        Assert.Equal("0 offers found today", verdict.Reason);

        var withErrors = HealthEvaluator.Evaluate(0, [100, 100, 100], ["timeout", "http 503"]);
        Assert.False(withErrors.Healthy);
        Assert.Equal("0 offers found today (2 error(s))", withErrors.Reason);
    }

    [Fact]
    public void Zero_offers_with_no_history_is_unhealthy()
    {
        var verdict = HealthEvaluator.Evaluate(0, [], NoErrors);

        Assert.False(verdict.Healthy);
        Assert.Equal("0 offers found today", verdict.Reason);
    }

    [Fact]
    public void Drop_below_twenty_percent_of_median_is_unhealthy()
    {
        // Median of [100, 100, 100] is 100; 19 is below 20 % of it.
        var verdict = HealthEvaluator.Evaluate(19, [100, 100, 100], NoErrors);

        Assert.False(verdict.Healthy);
        Assert.NotNull(verdict.Reason);
    }

    [Fact]
    public void Drop_with_fewer_than_three_previous_runs_is_healthy()
    {
        // Two previous runs are not enough history: a drop to 1 is still healthy.
        var verdict = HealthEvaluator.Evaluate(1, [100, 100], NoErrors);

        Assert.True(verdict.Healthy);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void Median_uses_only_the_last_seven()
    {
        // The first seven values (newest first) are 10..70, median 40; 0.2 × 40 = 8, and 9 is not below it: healthy.
        // The two older values (1000, 1000) are NOT used. If they were, the median of all nine would be 50
        // (0.2 × 50 = 10) and 9 would be unhealthy, so this test fails when the window is not applied.
        int[] previous = [10, 20, 30, 40, 50, 60, 70, 1000, 1000];

        var verdict = HealthEvaluator.Evaluate(9, previous, NoErrors);

        Assert.True(verdict.Healthy);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void Median_of_an_even_count_averages_the_two_middle_values()
    {
        // Sorted: 10, 20, 30, 40. Median = (20 + 30) / 2 = 25, so 0.2 × 25 = 5. Today 4 is unhealthy and the reason names 25.
        var verdict = HealthEvaluator.Evaluate(4, [40, 10, 30, 20], NoErrors);

        Assert.False(verdict.Healthy);
        Assert.Equal("4 offers found today, below 20% of the median 25 of the last 4 healthy runs", verdict.Reason);
    }

    [Fact]
    public void Exactly_twenty_percent_is_healthy()
    {
        // 0.2 × 100 = 20; the rule is strict, so 20 is healthy.
        var verdict = HealthEvaluator.Evaluate(20, [100, 100, 100], NoErrors);

        Assert.True(verdict.Healthy);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void Errors_do_not_change_the_verdict_when_offers_were_found()
    {
        var healthy = HealthEvaluator.Evaluate(50, [100, 100, 100], ["timeout"]);
        Assert.True(healthy.Healthy);
        Assert.Null(healthy.Reason);

        var drop = HealthEvaluator.Evaluate(5, [100, 100, 100], ["timeout"]);
        Assert.False(drop.Healthy);
        Assert.DoesNotContain("error", drop.Reason!);
    }

    [Fact]
    public void Reason_names_the_numbers_for_a_drop()
    {
        var verdict = HealthEvaluator.Evaluate(19, [100, 100, 100], NoErrors);

        Assert.False(verdict.Healthy);
        Assert.Equal("19 offers found today, below 20% of the median 100 of the last 3 healthy runs", verdict.Reason);
    }
}
