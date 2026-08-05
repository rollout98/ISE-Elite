using System;
using ISE.MarketOpen;
using Xunit;

namespace ISE.MarketOpen.Tests;

public sealed class MarketOpenEngineTests
{
    private readonly MarketOpenEngine _engine = new();

    [Fact]
    public void Premarket_observes_and_blocks_new_entries()
    {
        var decision = Evaluate(6, 45, false, true, true);
        Assert.Equal(MarketOpenPhase.PreMarket, decision.Phase);
        Assert.Equal(MarketOpenAction.Observe, decision.Action);
        Assert.False(decision.NewEntriesPermitted);
    }

    [Fact]
    public void Qualified_opening_range_setup_allows_entry()
    {
        var decision = Evaluate(8, 15, false, true, true);
        Assert.Equal(MarketOpenPhase.OpeningRange, decision.Phase);
        Assert.Equal(MarketOpenAction.AllowEntry, decision.Action);
        Assert.True(decision.NewEntriesPermitted);
    }

    [Fact]
    public void Reversal_window_supports_qualified_entry()
    {
        var decision = Evaluate(8, 50, false, true, true);
        Assert.Equal(MarketOpenPhase.ReversalWindow, decision.Phase);
        Assert.Equal(MarketOpenAction.AllowEntry, decision.Action);
    }

    [Fact]
    public void Pullback_window_supports_qualified_entry()
    {
        var decision = Evaluate(9, 45, false, true, true);
        Assert.Equal(MarketOpenPhase.PullbackWindow, decision.Phase);
        Assert.Equal(MarketOpenAction.AllowEntry, decision.Action);
    }

    [Fact]
    public void Existing_position_is_managed_after_entry_cutoff()
    {
        var decision = Evaluate(12, 30, true, true, false);
        Assert.Equal(MarketOpenPhase.TrendManagement, decision.Phase);
        Assert.Equal(MarketOpenAction.ManagePosition, decision.Action);
        Assert.False(decision.NewEntriesPermitted);
    }

    [Fact]
    public void Flat_account_stands_down_after_entry_cutoff()
    {
        var decision = Evaluate(10, 30, false, true, true);
        Assert.Equal(MarketOpenAction.StandDown, decision.Action);
        Assert.False(decision.NewEntriesPermitted);
    }

    [Fact]
    public void Authoritative_risk_block_forces_open_position_exit()
    {
        var decision = new MarketOpenEngine().Evaluate(new MarketOpenInput(
            new TimeSpan(9, 0, 0), true, true, true, authoritativeRiskBlock: true));
        Assert.Equal(MarketOpenAction.ForceExit, decision.Action);
        Assert.False(decision.NewEntriesPermitted);
    }

    [Fact]
    public void Three_pm_rule_forces_every_open_position_flat()
    {
        var decision = Evaluate(15, 0, true, true, true);
        Assert.Equal(MarketOpenPhase.ForceFlat, decision.Phase);
        Assert.Equal(MarketOpenAction.ForceExit, decision.Action);
        Assert.False(decision.NewEntriesPermitted);
    }

    private MarketOpenDecision Evaluate(int hour, int minute, bool positionOpen,
        bool openingRangeReady, bool setupReady)
        => _engine.Evaluate(new MarketOpenInput(new TimeSpan(hour, minute, 0),
            positionOpen, openingRangeReady, setupReady));
}
