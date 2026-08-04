using ISE.ExecutionOrchestrator;
using ISE.InstitutionalDecisionEngine;
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
