using System;

namespace ISE.OpportunityScoring;

/// <summary>Contains normalized evidence used to score one potential trade.</summary>
public sealed class OpportunityScoreInput
{
    /// <summary>Initializes a new opportunity score input.</summary>
    public OpportunityScoreInput(
        bool upstreamEligible,
        decimal trendScore,
        decimal structureScore,
        decimal liquidityScore,
        decimal orderFlowScore,
        decimal sessionScore,
        decimal setupBehaviorScore,
        decimal rewardRiskScore,
        bool newsBlocked = false)
    {
        UpstreamEligible = upstreamEligible;
        TrendScore = ValidateScore(trendScore, nameof(trendScore));
        StructureScore = ValidateScore(structureScore, nameof(structureScore));
        LiquidityScore = ValidateScore(liquidityScore, nameof(liquidityScore));
        OrderFlowScore = ValidateScore(orderFlowScore, nameof(orderFlowScore));
        SessionScore = ValidateScore(sessionScore, nameof(sessionScore));
        SetupBehaviorScore = ValidateScore(setupBehaviorScore, nameof(setupBehaviorScore));
        RewardRiskScore = ValidateScore(rewardRiskScore, nameof(rewardRiskScore));
        NewsBlocked = newsBlocked;
    }

    /// <summary>Gets whether all required upstream engines approved the candidate.</summary>
    public bool UpstreamEligible { get; }
    /// <summary>Gets the normalized trend score.</summary>
    public decimal TrendScore { get; }
    /// <summary>Gets the normalized structure score.</summary>
    public decimal StructureScore { get; }
    /// <summary>Gets the normalized liquidity score.</summary>
    public decimal LiquidityScore { get; }
    /// <summary>Gets the normalized order-flow score.</summary>
    public decimal OrderFlowScore { get; }
    /// <summary>Gets the normalized session score.</summary>
    public decimal SessionScore { get; }
    /// <summary>Gets the normalized setup-behavior score.</summary>
    public decimal SetupBehaviorScore { get; }
    /// <summary>Gets the normalized reward-to-risk score.</summary>
    public decimal RewardRiskScore { get; }
    /// <summary>Gets whether a news lockout blocks the opportunity.</summary>
    public bool NewsBlocked { get; }

    private static decimal ValidateScore(decimal value, string name)
    {
        if (value < 0m || value > 100m)
            throw new ArgumentOutOfRangeException(name, "Scores must be between zero and one hundred.");
        return value;
    }
}
