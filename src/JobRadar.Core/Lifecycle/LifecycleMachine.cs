using JobRadar.Core.Models;

namespace JobRadar.Core.Machine;

/// <summary>Events that move an offer through its lifecycle (SPEC §6.4 "Lifecycle machine").</summary>
public enum LifecycleEvent { Seen, Published, Missed, MissedThrice, Dead, Aged }

/// <summary>Outcome of one transition: <see cref="Ok"/> carries the next state, <see cref="Invalid"/> names the refused pair.</summary>
public abstract record LifecycleResult
{
    public sealed record Ok(Lifecycle Next) : LifecycleResult;

    public sealed record Invalid(string Reason) : LifecycleResult;
}

/// <summary>
/// The lifecycle table of SPEC §6.4, encoded exactly. The absent-offer row (first sight) is not encoded here: the store creates it.
/// Only the 12 pairs listed below are <see cref="LifecycleResult.Ok"/>; every other pair is <see cref="LifecycleResult.Invalid"/>.
/// Conditions of SPEC (source healthy, <c>expiry_by_absence</c>, <c>missed_runs ≥ 3</c>, liveness, <c>expired_at</c> age) are the caller's to check.
/// </summary>
public static class LifecycleMachine
{
    public static LifecycleResult Next(Lifecycle state, LifecycleEvent e) => (state, e) switch
    {
        (Lifecycle.New, LifecycleEvent.Seen) => new LifecycleResult.Ok(Lifecycle.New),
        (Lifecycle.Active, LifecycleEvent.Seen) => new LifecycleResult.Ok(Lifecycle.Active),
        (Lifecycle.Expired, LifecycleEvent.Seen) => new LifecycleResult.Ok(Lifecycle.Active),
        (Lifecycle.Archived, LifecycleEvent.Seen) => new LifecycleResult.Ok(Lifecycle.Archived),

        (Lifecycle.New, LifecycleEvent.Published) => new LifecycleResult.Ok(Lifecycle.Active),

        (Lifecycle.New, LifecycleEvent.Missed) => new LifecycleResult.Ok(Lifecycle.New),
        (Lifecycle.Active, LifecycleEvent.Missed) => new LifecycleResult.Ok(Lifecycle.Active),

        (Lifecycle.New, LifecycleEvent.MissedThrice) => new LifecycleResult.Ok(Lifecycle.Expired),
        (Lifecycle.Active, LifecycleEvent.MissedThrice) => new LifecycleResult.Ok(Lifecycle.Expired),

        (Lifecycle.New, LifecycleEvent.Dead) => new LifecycleResult.Ok(Lifecycle.Expired),
        (Lifecycle.Active, LifecycleEvent.Dead) => new LifecycleResult.Ok(Lifecycle.Expired),

        (Lifecycle.Expired, LifecycleEvent.Aged) => new LifecycleResult.Ok(Lifecycle.Archived),

        _ => new LifecycleResult.Invalid($"no transition from {state} on {e}"),
    };
}
