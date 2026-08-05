using ISE.RunnerIntelligence;
using ISE.TradeSupervisor;
using Xunit;

namespace ISE.TradeSupervisor.Tests;

public sealed class TradeSupervisorEngineTests
{
    private readonly TradeSupervisorEngine _engine = new();

    [Fact]
    public void Healthy_runner_continues_to_hold()
    {
        var result = _engine.Evaluate(Input(RunnerState.ConfirmedRunner, RunnerAction.Hold, 86));

        Assert.Equal(TradeSupervisorState.Hold, result.State);
        Assert.True(result.RunnerStillValid);
        Assert.False(result.ExitImmediately);
    }

    [Fact]
    public void Elite_runner_is_promoted()
    {
        var result = _engine.Evaluate(Input(RunnerState.EliteRunner, RunnerAction.Promote, 94));

        Assert.Equal(TradeSupervisorState.PromoteRunner, result.State);
        Assert.True(result.CanScale);
        Assert.True(result.RunnerStillValid);
    }

    [Fact]
    public void Rising_risk_pressure_protects_position()
    {
        var result = _engine.Evaluate(Input(RunnerState.ConfirmedRunner, RunnerAction.Hold, 82,
            riskPressureScore: 76m));

        Assert.Equal(TradeSupervisorState.Protect, result.State);
        Assert.True(result.TightenStops);
    }

    [Fact]
    public void Large_profit_with_weaker_persistence_tightens_stop()
    {
        var result = _engine.Evaluate(Input(RunnerState.PossibleRunner, RunnerAction.Protect, 70,
            openProfitR: 2.5m));

        Assert.Equal(TradeSupervisorState.TightenStop, result.State);
        Assert.True(result.TightenStops);
    }

    [Fact]
    public void Weakening_structure_reduces_position()
    {
        var result = _engine.Evaluate(Input(RunnerState.ConfirmedRunner, RunnerAction.Hold, 80,
            structureWeakening: true));

        Assert.Equal(TradeSupervisorState.Reduce, result.State);
        Assert.False(result.RunnerStillValid);
    }

    [Fact]
    public void Institutional_reversal_exits_position()
    {
        var result = _engine.Evaluate(Input(RunnerState.Reversal, RunnerAction.Exit, 20,
            institutionalReversal: true));

        Assert.Equal(TradeSupervisorState.Exit, result.State);
        Assert.True(result.ExitImmediately);
        Assert.False(result.ThesisStillValid);
    }

    [Fact]
    public void Authoritative_risk_block_forces_exit()
    {
        var result = _engine.Evaluate(Input(RunnerState.EliteRunner, RunnerAction.Promote, 96,
            authoritativeRiskBlock: true));

        Assert.Equal(TradeSupervisorState.Exit, result.State);
        Assert.True(result.ExitImmediately);
    }

    [Fact]
    public void End_of_day_window_forces_flat()
    {
        var result = _engine.Evaluate(Input(RunnerState.EliteRunner, RunnerAction.Promote, 96,
            minutesUntilForceFlat: 5));

        Assert.Equal(TradeSupervisorState.ForceExit, result.State);
        Assert.True(result.ExitImmediately);
        Assert.Equal(100, result.Confidence);
    }

    private static TradeSupervisorInput Input(
        RunnerState runnerState,
        RunnerAction runnerAction,
        int persistence,
        bool thesisStillValid = true,
        bool institutionalReversal = false,
        bool structureWeakening = false,
        decimal openProfitR = 1m,
        decimal riskPressureScore = 20m,
        int minutesUntilForceFlat = 60,
        bool authoritativeRiskBlock = false)
        => new(
            runnerState,
            runnerAction,
            persistence,
            thesisStillValid,
            institutionalReversal,
            structureWeakening,
            openProfitR,
            riskPressureScore,
            minutesUntilForceFlat,
            positionOpen: true,
            authoritativeRiskBlock);
}
