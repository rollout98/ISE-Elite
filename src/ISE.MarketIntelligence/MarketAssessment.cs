using System;
using System.Collections.Generic;

namespace ISE.MarketIntelligence;

/// <summary>Represents the unified, explainable assessment of current market conditions.</summary>
public sealed class MarketAssessment
{
    /// <summary>Initializes a market assessment.</summary>
    public MarketAssessment(
        MarketRegime regime,
        AuctionState auction,
        LiquidityEnvironment liquidity,
        VolatilityRegime volatility,
        InstitutionalBias institutionalBias,
        MarketHealth health,
        RecommendedEnvironment recommendedEnvironment,
        decimal qualityScore,
        decimal riskModifier,
        IReadOnlyList<string> reasons)
    {
        if (qualityScore < 0m || qualityScore > 100m)
            throw new ArgumentOutOfRangeException(nameof(qualityScore));
        if (riskModifier < 0m || riskModifier > 1m)
            throw new ArgumentOutOfRangeException(nameof(riskModifier));

        Regime = regime;
        Auction = auction;
        Liquidity = liquidity;
        Volatility = volatility;
        InstitutionalBias = institutionalBias;
        Health = health;
        RecommendedEnvironment = recommendedEnvironment;
        QualityScore = qualityScore;
        RiskModifier = riskModifier;
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    /// <summary>Gets the dominant market regime.</summary>
    public MarketRegime Regime { get; }
    /// <summary>Gets the auction state.</summary>
    public AuctionState Auction { get; }
    /// <summary>Gets the liquidity environment.</summary>
    public LiquidityEnvironment Liquidity { get; }
    /// <summary>Gets the volatility regime.</summary>
    public VolatilityRegime Volatility { get; }
    /// <summary>Gets inferred institutional bias.</summary>
    public InstitutionalBias InstitutionalBias { get; }
    /// <summary>Gets overall market health.</summary>
    public MarketHealth Health { get; }
    /// <summary>Gets the recommended strategy environment.</summary>
    public RecommendedEnvironment RecommendedEnvironment { get; }
    /// <summary>Gets the assessment quality score from zero to one hundred.</summary>
    public decimal QualityScore { get; }
    /// <summary>Gets the recommended risk multiplier from zero to one.</summary>
    public decimal RiskModifier { get; }
    /// <summary>Gets explainable assessment reasons.</summary>
    public IReadOnlyList<string> Reasons { get; }
}
