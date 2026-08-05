using ISE.MarketOpen;
using ISE.ORBIntelligence;
using Xunit;

namespace ISE.MarketNarrative.Tests;

public sealed class MarketNarrativeEngineTests
{
    private readonly MarketNarrativeEngine _engine = new();

    [Fact]
    public void Neutral_market_produces_balance_narrative()
    {
        var decision = _engine.Evaluate(Input(ORBState.AwaitBreakout, ORBDirection.None, 45m, 45m, 45m));
        Assert.Equal(NarrativeBias.Neutral, decision.Bias);
        Assert.Equal(NarrativePhase.Balance, decision.Phase);
    }

    [Fact]
    public void Strong_orb_breakout_creates_bullish_expansion()
    {
        var decision = _engine.Evaluate(Input(ORBState.BreakoutConfirmed, ORBDirection.Long, 90m, 92m, 88m, acceptance: true));
        Assert.Equal(NarrativeBias.Bullish, decision.Bias);
        Assert.Equal(NarrativePhase.Expansion, decision.Phase);
        Assert.True(decision.TrendHealthy);
    }

    [Fact]
    public void Failed_breakout_creates_reversal_narrative()
    {
        var decision = _engine.Evaluate(Input(ORBState.BreakoutRejected, ORBDirection.Long, 75m, 60m, 65m, rejection: true));
        Assert.Equal(NarrativeBias.Bearish, decision.Bias);
        Assert.Equal(NarrativePhase.Reversal, decision.Phase);
    }

    [Fact]
    public void Liquidity_sweep_with_acceptance_creates_continuation()
    {
        var decision = _engine.Evaluate(Input(ORBState.LiquiditySweep, ORBDirection.Long, 88m, 86m, 90m, acceptance: true));
        Assert.Equal(NarrativeBias.Bullish, decision.Bias);
        Assert.Equal(NarrativePhase.Continuation, decision.Phase);
    }

    [Fact]
    public void Liquidity_sweep_with_rejection_creates_distribution()
    {
        var decision = _engine.Evaluate(Input(ORBState.LiquiditySweep, ORBDirection.Long, 80m, 70m, 75m, rejection: true));
        Assert.Equal(NarrativeBias.Bearish, decision.Bias);
        Assert.Equal(NarrativePhase.Distribution, decision.Phase);
    }

    [Fact]
    public void Reinforcing_evidence_increases_narrative_strength()
    {
        var weak = _engine.Evaluate(Input(ORBState.BreakoutConfirmed, ORBDirection.Long, 55m, 55m, 55m));
        var strong = _engine.Evaluate(Input(ORBState.BreakoutConfirmed, ORBDirection.Long, 90m, 90m, 90m, acceptance: true, continuation: true));
        Assert.True(strong.Strength > weak.Strength);
    }

    [Fact]
    public void Contradictory_evidence_reduces_strength()
    {
        var aligned = _engine.Evaluate(Input(ORBState.BreakoutConfirmed, ORBDirection.Long, 85m, 85m, 85m, acceptance: true));
        var contradictory = _engine.Evaluate(Input(ORBState.BreakoutConfirmed, ORBDirection.Long, 85m, 85m, 85m, rejection: true));
        Assert.True(contradictory.Strength < aligned.Strength);
    }

    [Fact]
    public void Authoritative_risk_block_overrides_narrative()
    {
        var decision = _engine.Evaluate(Input(ORBState.BreakoutConfirmed, ORBDirection.Long, 99m, 99m, 99m, acceptance: true, riskBlock: true));
        Assert.Equal(NarrativePhase.StandAside, decision.Phase);
        Assert.Equal(0, decision.Strength);
        Assert.False(decision.RunnerLikely);
    }

    private static MarketNarrativeInput Input(
        ORBState orbState,
        ORBDirection direction,
        decimal structure,
        decimal orderFlow,
        decimal liquidity,
        bool acceptance = false,
        bool rejection = false,
        bool pullback = false,
        bool continuation = false,
        bool riskBlock = false)
        => new MarketNarrativeInput(
            MarketOpenPhase.OpeningRange,
            orbState,
            direction,
            structure,
            orderFlow,
            liquidity,
            acceptance,
            rejection,
            pullback,
            continuation,
            riskBlock);
}
