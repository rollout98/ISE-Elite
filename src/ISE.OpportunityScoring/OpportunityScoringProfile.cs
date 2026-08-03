using System;

namespace ISE.OpportunityScoring;

/// <summary>Defines component weights and grade thresholds for opportunity scoring.</summary>
public sealed class OpportunityScoringProfile
{
    /// <summary>Initializes a new scoring profile.</summary>
    public OpportunityScoringProfile(
        decimal trendWeight = 20m,
        decimal structureWeight = 15m,
        decimal liquidityWeight = 15m,
        decimal orderFlowWeight = 20m,
        decimal sessionWeight = 10m,
        decimal setupBehaviorWeight = 10m,
        decimal rewardRiskWeight = 10m,
        decimal bThreshold = 70m,
        decimal aThreshold = 80m,
        decimal eliteThreshold = 90m)
    {
        TrendWeight = ValidateWeight(trendWeight, nameof(trendWeight));
        StructureWeight = ValidateWeight(structureWeight, nameof(structureWeight));
        LiquidityWeight = ValidateWeight(liquidityWeight, nameof(liquidityWeight));
        OrderFlowWeight = ValidateWeight(orderFlowWeight, nameof(orderFlowWeight));
        SessionWeight = ValidateWeight(sessionWeight, nameof(sessionWeight));
        SetupBehaviorWeight = ValidateWeight(setupBehaviorWeight, nameof(setupBehaviorWeight));
        RewardRiskWeight = ValidateWeight(rewardRiskWeight, nameof(rewardRiskWeight));

        var total = TrendWeight + StructureWeight + LiquidityWeight + OrderFlowWeight + SessionWeight + SetupBehaviorWeight + RewardRiskWeight;
        if (total <= 0m)
            throw new ArgumentException("At least one scoring weight must be greater than zero.");
        if (bThreshold < 0m || eliteThreshold > 100m || bThreshold >= aThreshold || aThreshold >= eliteThreshold)
            throw new ArgumentException("Grade thresholds must be ordered within the range zero through one hundred.");

        BThreshold = bThreshold;
        AThreshold = aThreshold;
        EliteThreshold = eliteThreshold;
    }

    /// <summary>Gets the trend weight.</summary>
    public decimal TrendWeight { get; }
    /// <summary>Gets the market-structure weight.</summary>
    public decimal StructureWeight { get; }
    /// <summary>Gets the liquidity weight.</summary>
    public decimal LiquidityWeight { get; }
    /// <summary>Gets the order-flow weight.</summary>
    public decimal OrderFlowWeight { get; }
    /// <summary>Gets the session-quality weight.</summary>
    public decimal SessionWeight { get; }
    /// <summary>Gets the setup-behavior weight.</summary>
    public decimal SetupBehaviorWeight { get; }
    /// <summary>Gets the reward-to-risk weight.</summary>
    public decimal RewardRiskWeight { get; }
    /// <summary>Gets the minimum B-grade score.</summary>
    public decimal BThreshold { get; }
    /// <summary>Gets the minimum A-grade score.</summary>
    public decimal AThreshold { get; }
    /// <summary>Gets the minimum Elite-grade score.</summary>
    public decimal EliteThreshold { get; }

    private static decimal ValidateWeight(decimal value, string name)
    {
        if (value < 0m)
            throw new ArgumentOutOfRangeException(name, "Weights cannot be negative.");
        return value;
    }
}
