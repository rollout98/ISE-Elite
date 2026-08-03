using ISE.OpportunityScoring;
using Xunit;

namespace ISE.OpportunityScoring.Tests;

public sealed class OpportunityScoringEngineTests
{
    private readonly OpportunityScoringEngine _engine = new OpportunityScoringEngine(new OpportunityScoringProfile());

    [Fact]
    public void Strong_aligned_evidence_produces_elite_grade()
    {
        var result = _engine.Evaluate(new OpportunityScoreInput(true, 95m, 92m, 94m, 96m, 90m, 93m, 88m));

        Assert.Equal(OpportunityGrade.Elite, result.Grade);
        Assert.True(result.Eligible);
        Assert.Equal(1m, result.SizeMultiplier);
        Assert.True(result.Score >= 90m);
    }

    [Fact]
    public void Moderate_evidence_produces_reduced_size_b_grade()
    {
        var result = _engine.Evaluate(new OpportunityScoreInput(true, 75m, 72m, 70m, 76m, 74m, 71m, 73m));

        Assert.Equal(OpportunityGrade.B, result.Grade);
        Assert.True(result.Eligible);
        Assert.Equal(0.5m, result.SizeMultiplier);
    }

    [Fact]
    public void Weak_evidence_is_rejected()
    {
        var result = _engine.Evaluate(new OpportunityScoreInput(true, 60m, 62m, 58m, 64m, 65m, 59m, 61m));

        Assert.Equal(OpportunityGrade.Reject, result.Grade);
        Assert.False(result.Eligible);
        Assert.Equal(0m, result.SizeMultiplier);
    }

    [Fact]
    public void News_block_overrides_high_score()
    {
        var result = _engine.Evaluate(new OpportunityScoreInput(true, 100m, 100m, 100m, 100m, 100m, 100m, 100m, true));

        Assert.Equal(OpportunityGrade.Reject, result.Grade);
        Assert.False(result.Eligible);
        Assert.Equal(0m, result.Score);
    }
}
