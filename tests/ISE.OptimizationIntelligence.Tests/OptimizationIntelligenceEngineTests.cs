using System;
using System.Collections.Generic;
using ISE.OptimizationIntelligence;
using ISE.ReplaySession;
using Xunit;

namespace ISE.OptimizationIntelligence.Tests;

public sealed class OptimizationIntelligenceEngineTests
{
    private readonly OptimizationIntelligenceEngine _engine = new();

    [Fact]
    public void Stronger_candidate_replaces_weaker_candidate()
    {
        var result = _engine.Evaluate(new[]
        {
            Candidate("weak", 1, Report(10, 5, 5, 1m, 0.50m, 1.2m, 2m, 70m)),
            Candidate("strong", 2, Report(10, 7, 3, 5m, 0.70m, 2.5m, 1m, 90m))
        });

        Assert.Equal(OptimizationStatus.Ready, result.Status);
        var best = Assert.IsType<OptimizationScore>(result.Best);
        Assert.Equal("strong", best.Candidate.CandidateId);
    }

    [Fact]
    public void Lower_drawdown_wins_when_other_metrics_are_equal()
    {
        var result = _engine.Evaluate(new[]
        {
            Candidate("high-dd", 1, Report(10, 6, 4, 4m, 0.60m, 2m, 4m, 85m)),
            Candidate("low-dd", 2, Report(10, 6, 4, 4m, 0.60m, 2m, 1m, 85m))
        });

        var best = Assert.IsType<OptimizationScore>(result.Best);
        Assert.Equal("low-dd", best.Candidate.CandidateId);
    }

    [Fact]
    public void Higher_profit_factor_increases_composite_score()
    {
        var result = _engine.Evaluate(new[]
        {
            Candidate("low-pf", 1, Report(10, 6, 4, 3m, 0.60m, 1.2m, 1m, 85m)),
            Candidate("high-pf", 2, Report(10, 6, 4, 3m, 0.60m, 3m, 1m, 85m))
        });

        Assert.True(result.RankedCandidates[0].CompositeScore > result.RankedCandidates[1].CompositeScore);
        var best = Assert.IsType<OptimizationScore>(result.Best);
        Assert.Equal("high-pf", best.Candidate.CandidateId);
    }

    [Fact]
    public void Duplicate_parameter_sets_are_ignored()
    {
        var result = _engine.Evaluate(new[]
        {
            Candidate("first", 1, Report(10, 6, 4, 3m, 0.60m, 2m, 1m, 85m)),
            Candidate("duplicate", 1, Report(10, 9, 1, 9m, 0.90m, 5m, 0.2m, 99m))
        });

        Assert.Equal(1, result.DuplicateCandidatesIgnored);
        Assert.Single(result.RankedCandidates);
        var best = Assert.IsType<OptimizationScore>(result.Best);
        Assert.Equal("first", best.Candidate.CandidateId);
    }

    [Fact]
    public void Insufficient_sample_cannot_be_selected_as_best()
    {
        var result = _engine.Evaluate(new[]
        {
            Candidate("tiny", 1, Report(2, 2, 0, 8m, 1m, decimal.MaxValue, 0m, 100m)),
            Candidate("valid", 2, Report(5, 3, 2, 2m, 0.60m, 1.5m, 1m, 80m))
        }, minimumCompletedTrades: 3);

        var best = Assert.IsType<OptimizationScore>(result.Best);
        Assert.Equal("valid", best.Candidate.CandidateId);
        Assert.False(result.RankedCandidates[1].EvidenceSufficient);
    }

    [Fact]
    public void Ranking_is_deterministic_when_scores_tie()
    {
        var report = Report(10, 6, 4, 3m, 0.60m, 2m, 1m, 85m);
        var result = _engine.Evaluate(new[]
        {
            Candidate("beta", 2, report),
            Candidate("alpha", 1, report)
        });

        var best = Assert.IsType<OptimizationScore>(result.Best);
        Assert.Equal("alpha", best.Candidate.CandidateId);
        Assert.Equal("alpha", result.RankedCandidates[0].Candidate.CandidateId);
    }

    private static OptimizationCandidate Candidate(string id, decimal threshold, ReplaySessionReport report)
        => new OptimizationCandidate(id,
            new OptimizationParameterSet(new[]
            {
                new KeyValuePair<string, decimal>("ConfidenceThreshold", threshold)
            }), report);

    private static ReplaySessionReport Report(int trades, int winners, int losers,
        decimal totalR, decimal winRate, decimal profitFactor, decimal drawdown,
        decimal quality)
    {
        var metrics = new ReplaySessionMetrics(
            snapshotsEvaluated: trades,
            completedTrades: trades,
            winners: winners,
            losers: losers,
            totalResultR: totalR,
            winRate: winRate,
            profitFactor: profitFactor,
            maximumDrawdownR: drawdown,
            averageDecisionQuality: quality,
            correctDecisions: trades,
            partialDecisions: 0,
            incorrectDecisions: 0,
            blockedDecisions: 0);

        return new ReplaySessionReport("session", "NQ",
            Array.Empty<ReplaySessionStep>(), metrics,
            Array.Empty<ISE.InstitutionalMemory.InstitutionalTradeRecord>());
    }
}
