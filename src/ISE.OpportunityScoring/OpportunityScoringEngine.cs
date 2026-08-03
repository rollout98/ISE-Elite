using System;

namespace ISE.OpportunityScoring;

/// <summary>Calculates a weighted, explainable quality score for a potential trade.</summary>
public sealed class OpportunityScoringEngine
{
    private readonly OpportunityScoringProfile _profile;

    /// <summary>Initializes a new opportunity scoring engine.</summary>
    public OpportunityScoringEngine(OpportunityScoringProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    /// <summary>Scores one potential trade and assigns its grade and size multiplier.</summary>
    public OpportunityScoreSnapshot Evaluate(OpportunityScoreInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (!input.UpstreamEligible)
            return new OpportunityScoreSnapshot(0m, OpportunityGrade.Reject, 0m, false, "Required upstream approval is missing.");
        if (input.NewsBlocked)
            return new OpportunityScoreSnapshot(0m, OpportunityGrade.Reject, 0m, false, "The opportunity is blocked by the news-control window.");

        var totalWeight = _profile.TrendWeight + _profile.StructureWeight + _profile.LiquidityWeight
            + _profile.OrderFlowWeight + _profile.SessionWeight + _profile.SetupBehaviorWeight
            + _profile.RewardRiskWeight;

        var weightedTotal = input.TrendScore * _profile.TrendWeight
            + input.StructureScore * _profile.StructureWeight
            + input.LiquidityScore * _profile.LiquidityWeight
            + input.OrderFlowScore * _profile.OrderFlowWeight
            + input.SessionScore * _profile.SessionWeight
            + input.SetupBehaviorScore * _profile.SetupBehaviorWeight
            + input.RewardRiskScore * _profile.RewardRiskWeight;

        var score = Math.Round(weightedTotal / totalWeight, 2, MidpointRounding.AwayFromZero);

        if (score >= _profile.EliteThreshold)
            return new OpportunityScoreSnapshot(score, OpportunityGrade.Elite, 1m, true, "Elite opportunity: full approved size is permitted.");
        if (score >= _profile.AThreshold)
            return new OpportunityScoreSnapshot(score, OpportunityGrade.A, 1m, true, "A-grade opportunity: normal approved size is permitted.");
        if (score >= _profile.BThreshold)
            return new OpportunityScoreSnapshot(score, OpportunityGrade.B, 0.5m, true, "B-grade opportunity: reduced size is recommended.");

        return new OpportunityScoreSnapshot(score, OpportunityGrade.Reject, 0m, false, "The weighted opportunity score is below the minimum threshold.");
    }
}
