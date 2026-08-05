using ISE.IntegratedTradingBrainV2;
using ISE.MarketNarrative;
using ISE.ORBIntelligence;
using ISE.PullbackIntelligence;
using ISE.RunnerIntelligence;
using ISE.TradeSupervisor;
using Xunit;

namespace ISE.IntegratedTradingBrainV2.Tests;

public sealed class IntegratedTradingBrainV2EngineTests
{
    private readonly IntegratedTradingBrainV2Engine _engine = new();

    [Fact]
    public void Qualified_bullish_entry_creates_long_decision()
    {
        var result = _engine.Evaluate(Input(NarrativeBias.Bullish));
        Assert.Equal(BrainAction.EnterLong, result.Action);
        Assert.True(result.EntryPermitted);
    }

    [Fact]
    public void Qualified_bearish_entry_creates_short_decision()
    {
        var result = _engine.Evaluate(Input(NarrativeBias.Bearish));
        Assert.Equal(BrainAction.EnterShort, result.Action);
    }

    [Fact]
    public void Incomplete_entry_evidence_stands_aside()
    {
        var result = _engine.Evaluate(Input(NarrativeBias.Bullish, entryWindowOpen: false));
        Assert.Equal(BrainAction.StandAside, result.Action);
        Assert.False(result.EntryPermitted);
    }

    [Fact]
    public void Open_position_uses_supervisor_hold()
    {
        var result = _engine.Evaluate(Input(NarrativeBias.Bullish, positionOpen: true, supervisor: TradeSupervisorState.Hold));
        Assert.Equal(BrainAction.Hold, result.Action);
    }

    [Fact]
    public void Elite_position_is_promoted_to_runner()
    {
        var result = _engine.Evaluate(Input(NarrativeBias.Bullish, positionOpen: true,
            supervisor: TradeSupervisorState.PromoteRunner, runner: RunnerState.EliteRunner));
        Assert.Equal(BrainAction.PromoteRunner, result.Action);
        Assert.Contains("runner", result.Explanation.Summary, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Supervisor_exit_invalidates_open_position()
    {
        var result = _engine.Evaluate(Input(NarrativeBias.Bullish, positionOpen: true, supervisor: TradeSupervisorState.Exit));
        Assert.Equal(BrainAction.Exit, result.Action);
        Assert.True(result.ExitImmediately);
    }

    [Fact]
    public void Risk_override_blocks_entry()
    {
        var result = _engine.Evaluate(Input(NarrativeBias.Bullish, riskBlock: true));
        Assert.Equal(BrainAction.Exit, result.Action);
        Assert.False(result.EntryPermitted);
    }

    [Fact]
    public void Force_flat_overrides_every_other_decision()
    {
        var result = _engine.Evaluate(Input(NarrativeBias.Bullish, positionOpen: true,
            supervisor: TradeSupervisorState.PromoteRunner, forceFlat: true));
        Assert.Equal(BrainAction.ForceExit, result.Action);
        Assert.Equal(100, result.Confidence);
        Assert.True(result.ExitImmediately);
    }

    private static IntegratedTradingBrainV2Input Input(
        NarrativeBias bias,
        bool positionOpen = false,
        bool entryWindowOpen = true,
        TradeSupervisorState supervisor = TradeSupervisorState.Hold,
        RunnerState runner = RunnerState.ConfirmedRunner,
        bool riskBlock = false,
        bool forceFlat = false)
        => new(
            bias,
            85,
            ORBState.RetestQualified,
            PullbackState.Retest,
            runner,
            supervisor,
            88,
            positionOpen,
            entryWindowOpen,
            riskBlock,
            forceFlat);
}
