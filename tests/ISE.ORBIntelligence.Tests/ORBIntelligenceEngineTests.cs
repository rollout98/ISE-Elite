using ISE.MarketOpen;
using Xunit;

namespace ISE.ORBIntelligence.Tests;

public sealed class ORBIntelligenceEngineTests
{
    private readonly ORBIntelligenceEngine _engine = new();

    [Fact]
    public void Opening_range_still_forming_blocks_entry()
    {
        var decision = _engine.Evaluate(Input(complete: false, price: 100m));
        Assert.Equal(ORBState.Forming, decision.State);
        Assert.False(decision.EntryPermitted);
    }

    [Fact]
    public void Completed_range_waits_for_breakout()
    {
        var decision = _engine.Evaluate(Input(price: 105m));
        Assert.Equal(ORBState.AwaitBreakout, decision.State);
        Assert.Equal(ORBDirection.None, decision.Direction);
    }

    [Fact]
    public void Strong_aligned_breakout_is_confirmed()
    {
        var decision = _engine.Evaluate(Input(price: 111m, distance: 8m, volume: 1.8m, orderFlow: 92m, liquidity: 90m, structure: 94m));
        Assert.Equal(ORBState.BreakoutConfirmed, decision.State);
        Assert.True(decision.EntryPermitted);
    }

    [Fact]
    public void Weak_breakout_is_rejected()
    {
        var decision = _engine.Evaluate(Input(price: 111m, distance: 5m, volume: 0.6m, orderFlow: 35m, liquidity: 40m, structure: 30m));
        Assert.Equal(ORBState.BreakoutRejected, decision.State);
        Assert.False(decision.EntryPermitted);
    }

    [Fact]
    public void Breakout_returning_inside_range_is_liquidity_sweep()
    {
        var decision = _engine.Evaluate(Input(price: 109m, distance: 6m, returnedInside: true));
        Assert.Equal(ORBState.LiquiditySweep, decision.State);
    }

    [Fact]
    public void Successful_retest_is_qualified()
    {
        var decision = _engine.Evaluate(Input(price: 112m, distance: 7m, volume: 1.7m, orderFlow: 88m, liquidity: 86m, structure: 90m, retestAttempted: true, retestHeld: true));
        Assert.Equal(ORBState.RetestQualified, decision.State);
        Assert.True(decision.EntryPermitted);
    }

    [Fact]
    public void Failed_retest_stands_aside()
    {
        var decision = _engine.Evaluate(Input(price: 112m, distance: 7m, volume: 1.7m, orderFlow: 88m, liquidity: 86m, structure: 90m, retestAttempted: true, retestHeld: false));
        Assert.Equal(ORBState.RetestFailed, decision.State);
        Assert.False(decision.EntryPermitted);
    }

    [Fact]
    public void Elite_breakout_is_runner_candidate()
    {
        var decision = _engine.Evaluate(Input(price: 114m, distance: 10m, volume: 2m, orderFlow: 98m, liquidity: 96m, structure: 98m));
        Assert.True(decision.RunnerCandidate);
    }

    private static ORBInput Input(bool complete = true, decimal price = 111m, decimal distance = 5m, decimal volume = 1.2m, decimal orderFlow = 70m, decimal liquidity = 70m, decimal structure = 70m, bool returnedInside = false, bool retestAttempted = false, bool retestHeld = false)
        => new ORBInput(MarketOpenPhase.OpeningRange, complete, 110m, 100m, price, distance, volume, orderFlow, liquidity, structure, returnedInside, retestAttempted, retestHeld);
}
