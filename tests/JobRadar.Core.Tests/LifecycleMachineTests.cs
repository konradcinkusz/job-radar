using JobRadar.Core.Machine;
using JobRadar.Core.Models;

namespace JobRadar.Core.Tests;

public class LifecycleMachineTests
{
    private static readonly (Lifecycle From, LifecycleEvent Event, Lifecycle To)[] OkRows =
    [
        (Lifecycle.New, LifecycleEvent.Seen, Lifecycle.New),
        (Lifecycle.Active, LifecycleEvent.Seen, Lifecycle.Active),
        (Lifecycle.Expired, LifecycleEvent.Seen, Lifecycle.Active),
        (Lifecycle.Archived, LifecycleEvent.Seen, Lifecycle.Archived),
        (Lifecycle.New, LifecycleEvent.Published, Lifecycle.Active),
        (Lifecycle.New, LifecycleEvent.Missed, Lifecycle.New),
        (Lifecycle.Active, LifecycleEvent.Missed, Lifecycle.Active),
        (Lifecycle.New, LifecycleEvent.MissedThrice, Lifecycle.Expired),
        (Lifecycle.Active, LifecycleEvent.MissedThrice, Lifecycle.Expired),
        (Lifecycle.New, LifecycleEvent.Dead, Lifecycle.Expired),
        (Lifecycle.Active, LifecycleEvent.Dead, Lifecycle.Expired),
        (Lifecycle.Expired, LifecycleEvent.Aged, Lifecycle.Archived),
    ];

    [Fact]
    public void Row_New_Seen_New()
    {
        Assert.Equal(new LifecycleResult.Ok(Lifecycle.New), LifecycleMachine.Next(Lifecycle.New, LifecycleEvent.Seen));
    }

    [Fact]
    public void Row_Active_Seen_Active()
    {
        Assert.Equal(new LifecycleResult.Ok(Lifecycle.Active), LifecycleMachine.Next(Lifecycle.Active, LifecycleEvent.Seen));
    }

    [Fact]
    public void Row_Expired_Seen_Active()
    {
        Assert.Equal(new LifecycleResult.Ok(Lifecycle.Active), LifecycleMachine.Next(Lifecycle.Expired, LifecycleEvent.Seen));
    }

    [Fact]
    public void Row_Archived_Seen_Archived()
    {
        Assert.Equal(new LifecycleResult.Ok(Lifecycle.Archived), LifecycleMachine.Next(Lifecycle.Archived, LifecycleEvent.Seen));
    }

    [Fact]
    public void Row_New_Published_Active()
    {
        Assert.Equal(new LifecycleResult.Ok(Lifecycle.Active), LifecycleMachine.Next(Lifecycle.New, LifecycleEvent.Published));
    }

    [Fact]
    public void Row_New_Missed_New()
    {
        Assert.Equal(new LifecycleResult.Ok(Lifecycle.New), LifecycleMachine.Next(Lifecycle.New, LifecycleEvent.Missed));
    }

    [Fact]
    public void Row_Active_Missed_Active()
    {
        Assert.Equal(new LifecycleResult.Ok(Lifecycle.Active), LifecycleMachine.Next(Lifecycle.Active, LifecycleEvent.Missed));
    }

    [Fact]
    public void Row_New_MissedThrice_Expired()
    {
        Assert.Equal(new LifecycleResult.Ok(Lifecycle.Expired), LifecycleMachine.Next(Lifecycle.New, LifecycleEvent.MissedThrice));
    }

    [Fact]
    public void Row_Active_MissedThrice_Expired()
    {
        Assert.Equal(new LifecycleResult.Ok(Lifecycle.Expired), LifecycleMachine.Next(Lifecycle.Active, LifecycleEvent.MissedThrice));
    }

    [Fact]
    public void Row_New_Dead_Expired()
    {
        Assert.Equal(new LifecycleResult.Ok(Lifecycle.Expired), LifecycleMachine.Next(Lifecycle.New, LifecycleEvent.Dead));
    }

    [Fact]
    public void Row_Active_Dead_Expired()
    {
        Assert.Equal(new LifecycleResult.Ok(Lifecycle.Expired), LifecycleMachine.Next(Lifecycle.Active, LifecycleEvent.Dead));
    }

    [Fact]
    public void Row_Expired_Aged_Archived()
    {
        Assert.Equal(new LifecycleResult.Ok(Lifecycle.Archived), LifecycleMachine.Next(Lifecycle.Expired, LifecycleEvent.Aged));
    }

    [Fact]
    public void Every_pair_is_either_ok_or_invalid()
    {
        var okCount = 0;
        foreach (var state in Enum.GetValues<Lifecycle>())
        {
            foreach (var e in Enum.GetValues<LifecycleEvent>())
            {
                var result = LifecycleMachine.Next(state, e);
                Assert.True(result is LifecycleResult.Ok or LifecycleResult.Invalid, $"{state}+{e} returned {result.GetType().Name}");
                if (result is LifecycleResult.Ok ok)
                {
                    okCount++;
                    Assert.Contains(OkRows, r => r.From == state && r.Event == e && r.To == ok.Next);
                }
                else
                {
                    Assert.IsType<LifecycleResult.Invalid>(result);
                }
            }
        }
        Assert.Equal(12, okCount);
    }

    [Fact]
    public void Archived_never_leaves_archived()
    {
        foreach (var e in Enum.GetValues<LifecycleEvent>())
        {
            var result = LifecycleMachine.Next(Lifecycle.Archived, e);
            Assert.True(
                result is LifecycleResult.Invalid || result == new LifecycleResult.Ok(Lifecycle.Archived),
                $"Archived+{e} returned {result}");
        }
    }
}
