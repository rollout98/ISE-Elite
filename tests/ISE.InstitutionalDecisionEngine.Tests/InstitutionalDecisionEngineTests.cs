using Xunit;

namespace ISE.InstitutionalDecisionEngine.Tests;

public sealed class InstitutionalDecisionEngineTests
{
    private readonly InstitutionalDecisionEngine _engine = new InstitutionalDecisionEngine();

    [Fact]
    public void Elite_alignment_produces_a_plus_execution()
    {
        var result = _engine.Evaluate(new InstitutionalDecisionInput
        {
            MarketQuality = 95,
            ContextQuality = 92,
            NarrativeQuality = 94,
            HistoricalEvidence = 90,
            TimeframeAlignment = 93,
            ExecutionQuality = 96,
            DecisionConfidence = 95,
            RiskMultiplier = 1.0
        });

        Assert.Equal(InstitutionalDecisionAction.Execute, result.Action);
        Assert.Equal(OpportunityGrade.APlus, result.Grade);
        Assert.Equal(1.0, result.ParticipationMultiplier);
    }

    [Fact]
    public void Good_but_imperfect_alignment_executes_at_reduced_size()
    {
        var result = _engine.Evaluate(new InstitutionalDecisionInput
        {
            MarketQuality = 78,
            ContextQuality = 76,
            NarrativeQuality = 80,
            HistoricalEvidence = 70,
            TimeframeAlignment = 74,
            ExecutionQuality = 72,
            DecisionConfidence = 79,
            RiskMultiplier = 0.65
        });

        Assert.Equal(InstitutionalDecisionAction.ExecuteReduced, result.Action);
        Assert.Equal(OpportunityGrade.B, result.Grade);
        Assert.True(result.ParticipationMultiplier <= 0.60);
    }

    [Fact]
    public void Transitioning_context_delays_participation()
    {
        var result = _engine.Evaluate(new InstitutionalDecisionInput
        {
            MarketQuality = 88,
            ContextQuality = 86,
            NarrativeQuality = 88,
            HistoricalEvidence = 82,
            TimeframeAlignment = 85,
            ExecutionQuality = 84,
            DecisionConfidence = 87,
            RiskMultiplier = 0.85,
            ContextTransitioning = true
        });

        Assert.NotEqual(InstitutionalDecisionAction.Execute, result.Action);
        Assert.Contains(result.Reasons, reason => reason.Contains("transitioning"));
    }

    [Fact]
    public void Authoritative_risk_block_overrides_all_evidence()
    {
        var result = _engine.Evaluate(new InstitutionalDecisionInput
        {
            MarketQuality = 100,
            ContextQuality = 100,
            NarrativeQuality = 100,
            HistoricalEvidence = 100,
            TimeframeAlignment = 100,
            ExecutionQuality = 100,
            DecisionConfidence = 100,
            RiskMultiplier = 1.0,
            AuthoritativeRiskBlock = true
        });

        Assert.Equal(InstitutionalDecisionAction.Blocked, result.Action);
        Assert.Equal(OpportunityGrade.Rejected, result.Grade);
        Assert.Equal(0, result.ParticipationMultiplier);
    }
}
