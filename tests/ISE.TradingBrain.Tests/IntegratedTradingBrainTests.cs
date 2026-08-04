using ISE.ExecutionOrchestrator;
using ISE.InstitutionalDecisionEngine;
using ISE.TradeManagementIntelligence;
using ISE.TradeStateIntelligence;
using Xunit;

namespace ISE.TradingBrain.Tests;

public sealed class IntegratedTradingBrainTests
{
    private readonly IntegratedTradingBrain _brain = new();

    [Fact]
    public void Elite_flat_setup_flows_through_to_sized_entry()
    {
        var decision = _brain.Evaluate(EliteInput());

        Assert.Equal(InstitutionalDecisionAction.Execute, decision.InstitutionalDecision.Action);
        Assert.True(decision.PositionSizingDecision.Contracts > 0);
        Assert.Equal(ExecutionOrchestrationAction.SubmitEntry, decision.ExecutionDecision.Action);
        Assert.Equal(decision.PositionSizingDecision.Contracts, decision.ExecutionDecision.Contracts);
    }

    [Fact]
    public void Weak_institutional_decision_never_reaches_entry_submission()
    {
        var input = EliteInput();
        input.InstitutionalDecision.MarketQuality = 40;
        input.InstitutionalDecision.ContextQuality = 35;
        input.InstitutionalDecision.ExecutionQuality = 45;

        var decision = _brain.Evaluate(input);

        Assert.NotEqual(ExecutionOrchestrationAction.SubmitEntry, decision.ExecutionDecision.Action);
        Assert.Equal(0, decision.ExecutionDecision.Contracts);
    }

    [Fact]
    public void Open_position_exit_directive_overrides_new_entry_path()
    {
        var input = EliteInput();
        input.PositionOpen = true;
        input.LiveTradeDirective = LiveTradeDirective.Exit;

        var decision = _brain.Evaluate(input);

        Assert.Equal(ExecutionOrchestrationAction.ExitPosition, decision.ExecutionDecision.Action);
        Assert.Equal(0, decision.ExecutionDecision.Contracts);
    }

    [Fact]
    public void Authoritative_block_propagates_across_pipeline()
    {
        var input = EliteInput();
        input.AuthoritativeRiskBlock = true;

        var decision = _brain.Evaluate(input);

        Assert.Equal(InstitutionalDecisionAction.Blocked, decision.InstitutionalDecision.Action);
        Assert.Equal(0, decision.PositionSizingDecision.Contracts);
        Assert.Equal(ExecutionOrchestrationAction.Blocked, decision.ExecutionDecision.Action);
    }

    [Fact]
    public void Invalidated_live_thesis_exits_immediately()
    {
        var input = HealthyOpenPosition();
        input.LiveTradeState.ThesisHealth = 25;
        input.LiveTradeManagement = Management(25, 70, 20, 0.20m, 10m, 8m);

        var decision = _brain.Evaluate(input);

        Assert.Equal(TradeStateAction.Exit, decision.TradeStateDecision.Action);
        Assert.Equal(TradeManagementAction.Exit, decision.TradeManagementDecision.Action);
        Assert.Equal(ExecutionOrchestrationAction.ExitPosition, decision.ExecutionDecision.Action);
    }

    [Fact]
    public void Hard_risk_block_overrides_profitable_live_trade()
    {
        var input = HealthyOpenPosition();
        input.AuthoritativeRiskBlock = true;
        input.LiveTradeState.TargetProgress = 90;
        input.LiveTradeManagement = Management(95, 95, 95, 0.90m, 100m, 2m, true, true);

        var decision = _brain.Evaluate(input);

        Assert.Equal(TradeStateAction.Blocked, decision.TradeStateDecision.Action);
        Assert.Equal(TradeManagementAction.Blocked, decision.TradeManagementDecision.Action);
        Assert.Equal(ExecutionOrchestrationAction.Blocked, decision.ExecutionDecision.Action);
    }

    [Fact]
    public void Open_position_prevents_second_entry_submission()
    {
        var decision = _brain.Evaluate(HealthyOpenPosition());

        Assert.NotEqual(ExecutionOrchestrationAction.SubmitEntry, decision.ExecutionDecision.Action);
        Assert.Equal(ExecutionOrchestrationAction.ManageHold, decision.ExecutionDecision.Action);
        Assert.Equal(0, decision.ExecutionDecision.Contracts);
    }

    [Fact]
    public void Strong_live_trade_remains_on_hold()
    {
        var decision = _brain.Evaluate(HealthyOpenPosition());

        Assert.Equal(TradeHealth.Strong, decision.TradeStateDecision.Health);
        Assert.Equal(TradeStateAction.Hold, decision.TradeStateDecision.Action);
        Assert.Equal(TradeManagementAction.Hold, decision.TradeManagementDecision.Action);
        Assert.Equal(ExecutionOrchestrationAction.ManageHold, decision.ExecutionDecision.Action);
    }

    [Fact]
    public void Profit_progress_triggers_position_protection()
    {
        var input = HealthyOpenPosition();
        input.LiveTradeState.TargetProgress = 55;
        input.LiveTradeState.FavorableExcursion = 50;
        input.LiveTradeManagement = Management(85, 80, 85, 0.50m, 50m, 5m, true, false);

        var decision = _brain.Evaluate(input);

        Assert.Equal(TradeStateAction.Protect, decision.TradeStateDecision.Action);
        Assert.Equal(TradeManagementAction.Protect, decision.TradeManagementDecision.Action);
        Assert.Equal(ExecutionOrchestrationAction.ManageProtect, decision.ExecutionDecision.Action);
    }

    [Fact]
    public void Favorable_continuation_triggers_trailing_protection()
    {
        var input = HealthyOpenPosition();
        input.LiveTradeState.TargetProgress = 90;
        input.LiveTradeState.FavorableExcursion = 95;
        input.LiveTradeManagement = Management(90, 88, 92, 0.80m, 95m, 5m, true, true);

        var decision = _brain.Evaluate(input);

        Assert.Equal(TradeStateAction.Trail, decision.TradeStateDecision.Action);
        Assert.Equal(TradeManagementAction.Trail, decision.TradeManagementDecision.Action);
        Assert.Equal(ExecutionOrchestrationAction.ManageTrail, decision.ExecutionDecision.Action);
    }

    [Fact]
    public void Weakening_live_evidence_reduces_exposure()
    {
        var input = HealthyOpenPosition();
        input.LiveTradeState.ThesisHealth = 48;
        input.LiveTradeState.MomentumHealth = 40;
        input.LiveTradeState.AdverseExcursion = 55;
        input.LiveTradeManagement = Management(50, 35, 65, 0.25m, 20m, 15m, true, false);

        var decision = _brain.Evaluate(input);

        Assert.Equal(TradeStateAction.Reduce, decision.TradeStateDecision.Action);
        Assert.Equal(TradeManagementAction.Reduce, decision.TradeManagementDecision.Action);
        Assert.Equal(ExecutionOrchestrationAction.ManageReduce, decision.ExecutionDecision.Action);
    }

    [Fact]
    public void More_protective_manual_directive_wins_over_derived_hold()
    {
        var input = HealthyOpenPosition();
        input.LiveTradeDirective = LiveTradeDirective.Reduce;

        var decision = _brain.Evaluate(input);

        Assert.Equal(TradeStateAction.Hold, decision.TradeStateDecision.Action);
        Assert.Equal(TradeManagementAction.Hold, decision.TradeManagementDecision.Action);
        Assert.Equal(ExecutionOrchestrationAction.ManageReduce, decision.ExecutionDecision.Action);
    }

    [Fact]
    public void Derived_exit_overrides_less_protective_manual_hold()
    {
        var input = HealthyOpenPosition();
        input.LiveTradeDirective = LiveTradeDirective.Hold;
        input.LiveTradeState.StructureIntegrity = 20;
        input.LiveTradeManagement = Management(20, 70, 20, 0.10m, 5m, 12m);

        var decision = _brain.Evaluate(input);

        Assert.Equal(ExecutionOrchestrationAction.ExitPosition, decision.ExecutionDecision.Action);
    }

    private static IntegratedTradingBrainInput HealthyOpenPosition()
    {
        var input = EliteInput();
        input.PositionOpen = true;
        input.LiveTradeState = new TradeStateInput
        {
            ThesisHealth = 90,
            MomentumHealth = 85,
            StructureIntegrity = 90,
            TargetProgress = 15,
            FavorableExcursion = 20,
            AdverseExcursion = 5
        };
        input.LiveTradeManagement = Management(90, 85, 90, 0.15m, 20m, 5m);
        return input;
    }

    private static TradeManagementInput Management(
        int thesis,
        int momentum,
        int structure,
        decimal targetProgress,
        decimal favorableExcursion,
        decimal adverseExcursion,
        bool breakEvenEligible = false,
        bool trailingEligible = false)
        => new TradeManagementInput(
            thesis,
            momentum,
            structure,
            targetProgress,
            favorableExcursion,
            adverseExcursion,
            breakEvenEligible,
            trailingEligible);

    private static IntegratedTradingBrainInput EliteInput() => new IntegratedTradingBrainInput
    {
        InstitutionalDecision = new InstitutionalDecisionInput
        {
            MarketQuality = 96,
            ContextQuality = 94,
            NarrativeQuality = 93,
            HistoricalEvidence = 90,
            TimeframeAlignment = 95,
            ExecutionQuality = 96,
            DecisionConfidence = 95,
            RiskMultiplier = 1.0
        },
        MaximumContracts = 4,
        AdaptiveRiskMultiplier = 1.0,
        StopDistanceRisk = 0,
        LiquidityCapacity = 100,
        AccountPressure = 0,
        ExecutionReady = true,
        OrderChannelAvailable = true
    };
}
