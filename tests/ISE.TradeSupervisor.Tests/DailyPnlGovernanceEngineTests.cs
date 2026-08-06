using ISE.TradeSupervisor;
using Xunit;

namespace ISE.TradeSupervisor.Tests;

public sealed class DailyPnlGovernanceEngineTests
{
    private readonly DailyPnlGovernanceEngine _engine = new();
    private readonly DailyPnlGovernancePolicy _policy = DailyPnlGovernancePolicy.ProductionDefault;

    [Fact]
    public void First_qualified_setup_receives_normal_risk_budget()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: 0m,
            setupQualified: true));

        Assert.Equal(DailyPnlGovernanceState.EntryEligible, result.State);
        Assert.True(result.NewEntriesPermitted);
        Assert.Equal(150m, result.MaximumNewTradeRisk);
    }

    [Fact]
    public void First_loss_requires_cooldown_before_second_attempt()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: -150m,
            completedTradeAttempts: 1,
            consecutiveLosses: 1,
            setupQualified: true,
            cooldownComplete: false));

        Assert.Equal(DailyPnlGovernanceState.Cooldown, result.State);
        Assert.False(result.NewEntriesPermitted);
    }

    [Fact]
    public void Second_qualified_attempt_is_allowed_after_cooldown()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: -150m,
            completedTradeAttempts: 1,
            consecutiveLosses: 1,
            setupQualified: true,
            cooldownComplete: true));

        Assert.Equal(DailyPnlGovernanceState.EntryEligible, result.State);
        Assert.True(result.NewEntriesPermitted);
        Assert.Equal(150m, result.MaximumNewTradeRisk);
    }

    [Fact]
    public void Two_consecutive_losses_lock_the_session()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: -300m,
            completedTradeAttempts: 2,
            consecutiveLosses: 2,
            setupQualified: true));

        Assert.Equal(DailyPnlGovernanceState.LossLockout, result.State);
        Assert.False(result.NewEntriesPermitted);
    }

    [Fact]
    public void Two_completed_attempts_lock_even_without_two_losses()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: 250m,
            completedTradeAttempts: 2,
            consecutiveLosses: 0,
            setupQualified: true));

        Assert.Equal(DailyPnlGovernanceState.TradeLimitLockout, result.State);
        Assert.False(result.NewEntriesPermitted);
    }

    [Fact]
    public void Three_hundred_dollar_green_day_rejects_non_exceptional_trade()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: 300m,
            completedTradeAttempts: 1,
            setupQualified: true,
            exceptionalSetup: false));

        Assert.Equal(DailyPnlGovernanceState.GreenDayProtection, result.State);
        Assert.False(result.NewEntriesPermitted);
        Assert.Equal(200m, result.ProtectedDailyPnlFloor);
    }

    [Fact]
    public void Exceptional_trade_at_three_hundred_is_capped_at_one_hundred_risk()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: 300m,
            completedTradeAttempts: 1,
            setupQualified: true,
            exceptionalSetup: true));

        Assert.Equal(DailyPnlGovernanceState.GreenDayProtection, result.State);
        Assert.True(result.NewEntriesPermitted);
        Assert.Equal(100m, result.MaximumNewTradeRisk);
        Assert.Equal(200m, result.ProtectedDailyPnlFloor);
    }

    [Fact]
    public void Green_day_risk_never_exceeds_normal_trade_budget()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: 450m,
            completedTradeAttempts: 1,
            setupQualified: true,
            exceptionalSetup: true));

        Assert.True(result.NewEntriesPermitted);
        Assert.Equal(150m, result.MaximumNewTradeRisk);
    }

    [Fact]
    public void Realized_five_hundred_locks_new_entries()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: 500m,
            setupQualified: true,
            exceptionalSetup: true));

        Assert.Equal(DailyPnlGovernanceState.ObjectiveReached, result.State);
        Assert.False(result.NewEntriesPermitted);
    }

    [Fact]
    public void Qualified_runner_may_continue_after_lower_objective()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: 100m,
            openPnl: 450m,
            positionOpen: true,
            runnerQualified: true));

        Assert.Equal(DailyPnlGovernanceState.ObjectiveReached, result.State);
        Assert.False(result.NewEntriesPermitted);
        Assert.True(result.ExistingRunnerMayContinue);
        Assert.True(result.ProtectOpenProfit);
        Assert.False(result.FlattenImmediately);
        Assert.Equal(500m, result.ProtectedDailyPnlFloor);
    }

    [Fact]
    public void Non_runner_is_flattened_after_lower_objective()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: 100m,
            openPnl: 450m,
            positionOpen: true,
            runnerQualified: false));

        Assert.Equal(DailyPnlGovernanceState.ObjectiveReached, result.State);
        Assert.True(result.FlattenImmediately);
    }

    [Fact]
    public void Upper_objective_flattens_and_locks()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: 250m,
            openPnl: 750m,
            positionOpen: true,
            runnerQualified: true));

        Assert.Equal(DailyPnlGovernanceState.UpperObjectiveReached, result.State);
        Assert.True(result.FlattenImmediately);
        Assert.False(result.ExistingRunnerMayContinue);
    }

    [Fact]
    public void Authoritative_risk_block_exits_open_position()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: 200m,
            openPnl: 100m,
            positionOpen: true,
            runnerQualified: true,
            authoritativeRiskBlock: true));

        Assert.Equal(DailyPnlGovernanceState.RiskLockout, result.State);
        Assert.True(result.FlattenImmediately);
        Assert.False(result.NewEntriesPermitted);
    }

    [Fact]
    public void No_qualified_setup_stands_aside()
    {
        var result = _engine.Evaluate(Input(
            realizedPnl: 0m,
            setupQualified: false));

        Assert.Equal(DailyPnlGovernanceState.Monitor, result.State);
        Assert.False(result.NewEntriesPermitted);
    }

    private DailyPnlGovernanceInput Input(
        decimal realizedPnl,
        decimal openPnl = 0m,
        int completedTradeAttempts = 0,
        int consecutiveLosses = 0,
        bool positionOpen = false,
        bool runnerQualified = false,
        bool setupQualified = false,
        bool exceptionalSetup = false,
        bool cooldownComplete = true,
        bool authoritativeRiskBlock = false,
        bool forceFlatWindow = false)
        => new DailyPnlGovernanceInput(
            _policy,
            realizedPnl,
            openPnl,
            completedTradeAttempts,
            consecutiveLosses,
            positionOpen,
            runnerQualified,
            setupQualified,
            exceptionalSetup,
            cooldownComplete,
            authoritativeRiskBlock,
            forceFlatWindow);
}
