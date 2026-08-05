using ISE.MarketNarrative;
using ISE.MarketOpen;
using ISE.ORBIntelligence;
using Xunit;

namespace ISE.PullbackIntelligence.Tests;

public sealed class PullbackIntelligenceEngineTests
{
    private readonly PullbackIntelligenceEngine _engine = new();

    [Fact]
    public void Shallow_pullback_in_strong_trend_is_healthy()
    {
        var decision = _engine.Evaluate(Input(depth: 28m, recovery: 82m));
        Assert.Equal(PullbackState.Healthy, decision.State);
        Assert.True(decision.EntryPermitted);
    }

    [Fact]
    public void Deep_pullback_with_strong_recovery_remains_valid()
    {
        var decision = _engine.Evaluate(Input(depth: 55m, recovery: 84m));
        Assert.Equal(PullbackState.DeepHealthy, decision.State);
        Assert.True(decision.EntryPermitted);
    }

    [Fact]
    public void Pullback_to_orb_breakout_level_is_retest()
    {
        var decision = _engine.Evaluate(Input(depth: 42m, recovery: 82m, touchedOrb: true));
        Assert.Equal(PullbackState.Retest, decision.State);
        Assert.True(decision.EntryPermitted);
    }

    [Fact]
    public void Weak_recovery_stands_aside()
    {
        var decision = _engine.Evaluate(Input(recovery: 38m, orderFlow: 40m));
        Assert.Equal(PullbackState.WeakRecovery, decision.State);
        Assert.False(decision.EntryPermitted);
    }

    [Fact]
    public void Structure_loss_is_trend_failure()
    {
        var decision = _engine.Evaluate(Input(structureBroken: true, positionOpen: true));
        Assert.Equal(PullbackState.TrendFailure, decision.State);
        Assert.True(decision.ExitImmediately);
    }

    [Fact]
    public void Opposing_institutional_flow_creates_reversal()
    {
        var decision = _engine.Evaluate(Input(opposingFlow: true, positionOpen: true));
        Assert.Equal(PullbackState.Reversal, decision.State);
        Assert.True(decision.ExitImmediately);
    }

    [Fact]
    public void Strong_continuation_keeps_runner_valid()
    {
        var decision = _engine.Evaluate(Input(depth: 32m, recovery: 94m,
            structure: 94m, orderFlow: 93m, liquidity: 92m,
            continuation: true, positionOpen: true));
        Assert.True(decision.RunnerStillValid);
        Assert.True(decision.AddToWinner);
    }

    [Fact]
    public void Authoritative_risk_block_forces_exit()
    {
        var decision = _engine.Evaluate(Input(positionOpen: true, riskBlock: true));
        Assert.Equal(PullbackState.StandAside, decision.State);
        Assert.True(decision.ExitImmediately);
        Assert.False(decision.EntryPermitted);
    }

    private static PullbackInput Input(
        decimal depth = 30m,
        decimal recovery = 80m,
        decimal structure = 85m,
        decimal orderFlow = 82m,
        decimal liquidity = 80m,
        bool touchedOrb = false,
        bool structureBroken = false,
        bool opposingFlow = false,
        bool continuation = false,
        bool positionOpen = false,
        bool riskBlock = false)
        => new PullbackInput(
            MarketOpenPhase.PullbackWindow,
            NarrativeBias.Bullish,
            NarrativePhase.Pullback,
            85,
            ORBDirection.Long,
            depth,
            recovery,
            structure,
            orderFlow,
            liquidity,
            touchedOrb,
            structureBroken,
            opposingFlow,
            continuation,
            positionOpen,
            riskBlock);
}
