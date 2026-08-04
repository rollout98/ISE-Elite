using System;
using ISE.InstitutionalDecisionEngine;
using ISE.MarketMemory;
using ISE.ReplayIntelligence;
using ISE.TradingBrain;
using Xunit;

namespace ISE.ReplaySession.Tests;

public sealed class ReplaySessionEngineTests
{
    private readonly ReplaySessionEngine _engine = new ReplaySessionEngine();

    [Fact]
    public void Empty_session_returns_zero_metrics()
    {
        var report = _engine.Evaluate(new ReplaySessionInput(
            "empty", "NQ", Array.Empty<ReplaySnapshot>()));

        Assert.Empty(report.Timeline);
        Assert.Empty(report.LearningRecords);
        Assert.Equal(0, report.Metrics.CompletedTrades);
        Assert.Equal(0m, report.Metrics.TotalResultR);
        Assert.Equal(0m, report.Metrics.MaximumDrawdownR);
    }

    [Fact]
    public void Snapshots_are_processed_in_chronological_order()
    {
        var later = Snapshot("later", new DateTime(2026, 8, 4, 8, 32, 0), true, false, false, 0m);
        var earlier = Snapshot("earlier", new DateTime(2026, 8, 4, 8, 31, 0), true, false, false, 0m);

        var report = _engine.Evaluate(new ReplaySessionInput("order", "NQ", new[] { later, earlier }));

        Assert.Equal("earlier", report.Timeline[0].Evaluation.Snapshot.SnapshotId);
        Assert.Equal("later", report.Timeline[1].Evaluation.Snapshot.SnapshotId);
        Assert.Equal(1, report.Timeline[0].Sequence);
        Assert.Equal(2, report.Timeline[1].Sequence);
    }

    [Fact]
    public void Completed_winner_updates_trade_and_win_metrics()
    {
        var winner = Snapshot("winner", new DateTime(2026, 8, 4, 8, 31, 0),
            true, false, true, 1.5m);

        var report = _engine.Evaluate(new ReplaySessionInput("winner-session", "NQ", new[] { winner }));

        Assert.Equal(1, report.Metrics.CompletedTrades);
        Assert.Equal(1, report.Metrics.Winners);
        Assert.Equal(0, report.Metrics.Losers);
        Assert.Equal(1m, report.Metrics.WinRate);
        Assert.Equal(1.5m, report.Metrics.TotalResultR);
        Assert.Single(report.LearningRecords);
    }

    [Fact]
    public void Consecutive_losses_calculate_peak_to_trough_drawdown()
    {
        var winner = Snapshot("one", new DateTime(2026, 8, 4, 8, 31, 0), true, false, true, 2m);
        var lossOne = Snapshot("two", new DateTime(2026, 8, 4, 8, 32, 0), true, false, true, -1m);
        var lossTwo = Snapshot("three", new DateTime(2026, 8, 4, 8, 33, 0), true, false, true, -1.5m);

        var report = _engine.Evaluate(new ReplaySessionInput(
            "drawdown", "NQ", new[] { winner, lossOne, lossTwo }));

        Assert.Equal(-0.5m, report.Metrics.TotalResultR);
        Assert.Equal(2.5m, report.Metrics.MaximumDrawdownR);
        Assert.Equal(2.5m, report.Timeline[2].RunningDrawdownR);
    }

    [Fact]
    public void Profit_factor_uses_gross_profit_over_gross_loss()
    {
        var first = Snapshot("one", new DateTime(2026, 8, 4, 8, 31, 0), true, false, true, 3m);
        var second = Snapshot("two", new DateTime(2026, 8, 4, 8, 32, 0), true, false, true, -1.5m);

        var report = _engine.Evaluate(new ReplaySessionInput(
            "pf", "NQ", new[] { first, second }));

        Assert.Equal(2m, report.Metrics.ProfitFactor);
        Assert.Equal(0.5m, report.Metrics.WinRate);
    }

    [Fact]
    public void Decision_quality_counts_are_aggregated()
    {
        var validEntry = Snapshot("valid", new DateTime(2026, 8, 4, 8, 31, 0), true, false, false, 0m);
        var invalidEntry = Snapshot("invalid", new DateTime(2026, 8, 4, 8, 32, 0), false, false, false, 0m);

        var report = _engine.Evaluate(new ReplaySessionInput(
            "quality", "NQ", new[] { validEntry, invalidEntry }));

        Assert.Equal(1, report.Metrics.CorrectDecisions);
        Assert.Equal(1, report.Metrics.IncorrectDecisions);
        Assert.Equal(50m, report.Metrics.AverageDecisionQuality);
    }

    private static ReplaySnapshot Snapshot(string id, DateTime timestamp,
        bool entryWasValid, bool exitWasRequired, bool completed, decimal resultR)
    {
        return new ReplaySnapshot(
            id,
            timestamp,
            new MarketFingerprint("NQ", "NewYork", "Trend", "OpeningDrive",
                "Continuation", "Normal", "Normal", "Initiative", 90),
            "OpeningDrive",
            "v3",
            EliteInput(),
            new ReplayObservedOutcome(entryWasValid, exitWasRequired, completed,
                resultR > 0, resultR, Math.Max(0m, resultR * 10m),
                resultR < 0 ? Math.Abs(resultR * 10m) : 2m, 12));
    }

    private static IntegratedTradingBrainInput EliteInput()
    {
        return new IntegratedTradingBrainInput
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
}
