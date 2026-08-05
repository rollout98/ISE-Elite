using ISE.MarketNarrative;
using ISE.PullbackIntelligence;
using Xunit;

namespace ISE.RunnerIntelligence.Tests;

public sealed class RunnerIntelligenceEngineTests
{
    private readonly RunnerIntelligenceEngine _engine = new();

    [Fact]
    public void Chop_is_not_promoted_to_runner()
    {
        var decision = _engine.Evaluate(Input(bias: NarrativeBias.Neutral, phase: NarrativePhase.Balance,
            narrative: 35, structure: 40, orderFlow: 42, liquidity: 38, momentum: 35));
        Assert.Equal(RunnerState.NotRunner, decision.State);
        Assert.False(decision.HoldPosition);
    }

    [Fact]
    public void Strong_developing_trend_becomes_possible_runner()
    {
        var decision = _engine.Evaluate(Input(narrative: 70, structure: 68, orderFlow: 66,
            liquidity: 64, momentum: 65, pullback: PullbackState.None));
        Assert.Equal(RunnerState.PossibleRunner, decision.State);
        Assert.True(decision.HoldPosition);
    }

    [Fact]
    public void Healthy_continuation_becomes_confirmed_runner()
    {
        var decision = _engine.Evaluate(Input(narrative: 84, structure: 86, orderFlow: 82,
            liquidity: 80, momentum: 83, pullback: PullbackState.Healthy, continuation: true));
        Assert.Equal(RunnerState.ConfirmedRunner, decision.State);
        Assert.Equal(RunnerAction.Hold, decision.Action);
    }

    [Fact]
    public void Multiple_healthy_continuations_become_elite_runner()
    {
        var decision = _engine.Evaluate(Input(narrative: 95, structure: 94, orderFlow: 92,
            liquidity: 90, momentum: 94, pullback: PullbackState.Retest,
            continuation: true, healthyPullbacks: 2));
        Assert.Equal(RunnerState.EliteRunner, decision.State);
        Assert.True(decision.AllowScaleIn);
    }

    [Fact]
    public void Exhaustion_removes_runner_status()
    {
        var decision = _engine.Evaluate(Input(exhaustion: true, continuation: true,
            pullback: PullbackState.Healthy));
        Assert.Equal(RunnerState.Exhaustion, decision.State);
        Assert.Equal(RunnerAction.Reduce, decision.Action);
    }

    [Fact]
    public void Institutional_reversal_exits_runner()
    {
        var decision = _engine.Evaluate(Input(institutionalReversal: true));
        Assert.Equal(RunnerState.Reversal, decision.State);
        Assert.True(decision.ExitImmediately);
    }

    [Fact]
    public void Authoritative_risk_block_exits_open_position()
    {
        var decision = _engine.Evaluate(Input(authoritativeRiskBlock: true));
        Assert.Equal(RunnerState.StandAside, decision.State);
        Assert.True(decision.ExitImmediately);
    }

    [Fact]
    public void Elite_runner_holds_through_healthy_pullback()
    {
        var decision = _engine.Evaluate(Input(narrative: 96, structure: 95, orderFlow: 94,
            liquidity: 92, momentum: 95, pullback: PullbackState.DeepHealthy,
            continuation: true, healthyPullbacks: 3));
        Assert.Equal(RunnerState.EliteRunner, decision.State);
        Assert.True(decision.HoldPosition);
        Assert.False(decision.TightenStop);
    }

    private static RunnerInput Input(
        NarrativeBias bias = NarrativeBias.Bullish,
        NarrativePhase phase = NarrativePhase.Continuation,
        int narrative = 85,
        PullbackState pullback = PullbackState.Healthy,
        int healthyPullbacks = 1,
        decimal structure = 85m,
        decimal orderFlow = 84m,
        decimal liquidity = 82m,
        decimal momentum = 84m,
        bool continuation = false,
        bool exhaustion = false,
        bool institutionalReversal = false,
        bool positionOpen = true,
        bool authoritativeRiskBlock = false)
        => new RunnerInput(bias, phase, narrative, pullback, healthyPullbacks, structure,
            orderFlow, liquidity, momentum, continuation, exhaustion,
            institutionalReversal, positionOpen, authoritativeRiskBlock);
}
