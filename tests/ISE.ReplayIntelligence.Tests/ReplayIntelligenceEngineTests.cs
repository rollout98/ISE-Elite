using System;
using ISE.ExecutionOrchestrator;
using ISE.InstitutionalDecisionEngine;
using ISE.MarketMemory;
using ISE.TradeManagementIntelligence;
using ISE.TradeStateIntelligence;
using ISE.TradingBrain;
using Xunit;

namespace ISE.ReplayIntelligence.Tests;

public sealed class ReplayIntelligenceEngineTests
{
    private readonly ReplayIntelligenceEngine _engine = new ReplayIntelligenceEngine();

    [Fact]
    public void Valid_historical_entry_is_graded_correct()
    {
        var evaluation = _engine.Evaluate(Snapshot(EliteInput(), true, false, true, 1.8m));

        Assert.Equal(ExecutionOrchestrationAction.SubmitEntry, evaluation.BrainDecision.ExecutionDecision.Action);
        Assert.Equal(ReplayDecisionQuality.Correct, evaluation.Quality);
        Assert.Equal(100, evaluation.QualityScore);
    }

    [Fact]
    public void Correctly_skipped_weak_entry_is_graded_correct()
    {
        var input = EliteInput();
        input.InstitutionalDecision.MarketQuality = 30;
        input.InstitutionalDecision.ContextQuality = 30;
        input.InstitutionalDecision.ExecutionQuality = 30;

        var evaluation = _engine.Evaluate(Snapshot(input, false, false, false, 0m));

        Assert.Equal(ExecutionOrchestrationAction.Wait, evaluation.BrainDecision.ExecutionDecision.Action);
        Assert.Equal(ReplayDecisionQuality.Correct, evaluation.Quality);
    }

    [Fact]
    public void Invalidated_open_trade_exit_is_graded_correct()
    {
        var input = HealthyOpenPosition();
        input.LiveTradeState.ThesisHealth = 20;
        input.LiveTradeState.StructureIntegrity = 20;
        input.LiveTradeManagement = Management(20, 60, 20, 0.20m, 10m, 15m);

        var evaluation = _engine.Evaluate(Snapshot(input, true, true, true, -0.4m));

        Assert.Equal(ExecutionOrchestrationAction.ExitPosition, evaluation.BrainDecision.ExecutionDecision.Action);
        Assert.Equal(ReplayDecisionQuality.Correct, evaluation.Quality);
    }

    [Fact]
    public void Protective_action_before_required_exit_is_partially_correct()
    {
        var input = HealthyOpenPosition();
        input.LiveTradeState.TargetProgress = 55;
        input.LiveTradeState.FavorableExcursion = 50;
        input.LiveTradeManagement = Management(85, 80, 85, 0.50m, 50m, 5m, true, false);

        var evaluation = _engine.Evaluate(Snapshot(input, true, true, true, 0.2m));

        Assert.Equal(ExecutionOrchestrationAction.ManageProtect, evaluation.BrainDecision.ExecutionDecision.Action);
        Assert.Equal(ReplayDecisionQuality.PartiallyCorrect, evaluation.Quality);
        Assert.Equal(60, evaluation.QualityScore);
    }

    [Fact]
    public void Completed_trade_produces_institutional_learning_record()
    {
        var evaluation = _engine.Evaluate(Snapshot(EliteInput(), true, false, true, 2.1m));

        var record = Assert.Single(evaluation.LearningRecords);
        Assert.Equal("OpenDrive", record.Playbook);
        Assert.Equal("v3.0", record.BrainVersion);
        Assert.Equal(2.1m, record.ResultR);
        Assert.True(record.ThesisConfirmed);
    }

    [Fact]
    public void Authoritative_block_is_preserved_in_replay()
    {
        var input = EliteInput();
        input.AuthoritativeRiskBlock = true;

        var evaluation = _engine.Evaluate(Snapshot(input, true, false, false, 0m));

        Assert.Equal(ExecutionOrchestrationAction.Blocked, evaluation.BrainDecision.ExecutionDecision.Action);
        Assert.Equal(ReplayDecisionQuality.Blocked, evaluation.Quality);
        Assert.Empty(evaluation.LearningRecords);
    }

    private static ReplaySnapshot Snapshot(IntegratedTradingBrainInput input, bool entryWasValid,
        bool exitWasRequired, bool tradeCompleted, decimal resultR)
        => new ReplaySnapshot(
            Guid.NewGuid().ToString("N"),
            new DateTime(2026, 8, 4, 8, 35, 0, DateTimeKind.Utc),
            new MarketFingerprint("MNQ", "NewYork", "Trending", "OpenDrive",
                "Bullish", "Supportive", "Normal", "Initiative", 92),
            "OpenDrive",
            "v3.0",
            input,
            new ReplayObservedOutcome(entryWasValid, exitWasRequired, tradeCompleted,
                true, resultR, 80m, 15m, 18));

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

    private static TradeManagementInput Management(int thesis, int momentum, int structure,
        decimal targetProgress, decimal favorableExcursion, decimal adverseExcursion,
        bool breakEvenEligible = false, bool trailingEligible = false)
        => new TradeManagementInput(thesis, momentum, structure, targetProgress,
            favorableExcursion, adverseExcursion, breakEvenEligible, trailingEligible);

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
