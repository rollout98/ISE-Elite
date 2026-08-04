using System;
using System.Collections.Generic;

namespace ISE.MarketIntelligence;

/// <summary>Synthesizes existing market evidence into one explainable assessment.</summary>
public sealed class MarketIntelligenceEngine
{
    /// <summary>Evaluates current market intelligence.</summary>
    public MarketAssessment Evaluate(MarketIntelligenceInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        var volatility = ClassifyVolatility(input.VolatilityLevel);
        var liquidity = ClassifyLiquidity(input);
        var auction = ClassifyAuction(input);
        var regime = ClassifyRegime(input, volatility);
        var institutionalBias = ClassifyInstitutionalBias(input);
        var qualityScore = CalculateQuality(input, volatility, liquidity);
        var health = ClassifyHealth(qualityScore, volatility, liquidity);
        var environment = RecommendEnvironment(regime, health, volatility, liquidity);
        var riskModifier = CalculateRiskModifier(health, volatility, liquidity);
        var reasons = BuildReasons(regime, auction, liquidity, volatility, institutionalBias, health, environment, qualityScore);

        return new MarketAssessment(
            regime,
            auction,
            liquidity,
            volatility,
            institutionalBias,
            health,
            environment,
            qualityScore,
            riskModifier,
            reasons);
    }

    private static MarketRegime ClassifyRegime(MarketIntelligenceInput input, VolatilityRegime volatility)
    {
        if (input.RejectionQuality >= 0.8m && input.BreakoutQuality >= 0.65m)
            return MarketRegime.BreakoutFailure;
        if (input.RejectionQuality >= 0.8m && input.DirectionalStrength >= 0.65m)
            return MarketRegime.Reversal;
        if (input.BreakoutQuality >= 0.8m && input.DirectionalStrength >= 0.7m)
            return MarketRegime.Breakout;
        if (input.BalanceQuality >= 0.8m && input.DirectionalStrength <= 0.4m)
            return MarketRegime.BalancedAuction;
        if (input.BalanceQuality >= 0.65m && input.TrendPersistence <= 0.45m)
            return MarketRegime.Rotational;
        if (input.DirectionalStrength >= 0.75m && input.TrendPersistence >= 0.7m && volatility >= VolatilityRegime.Normal)
            return MarketRegime.TrendExpansion;
        if (input.TrendPersistence >= 0.65m && input.DirectionalStrength >= 0.45m)
            return MarketRegime.TrendPullback;
        if (input.BalanceQuality >= 0.55m)
            return MarketRegime.RangeBound;
        return MarketRegime.Indeterminate;
    }

    private static AuctionState ClassifyAuction(MarketIntelligenceInput input)
    {
        if (input.RejectionQuality >= 0.75m)
            return AuctionState.Rejection;
        if (input.BreakoutQuality >= 0.75m && input.DirectionalStrength >= 0.65m)
            return AuctionState.PriceDiscovery;
        if (input.BalanceQuality >= 0.75m)
            return AuctionState.Balanced;
        if (input.TrendPersistence >= 0.65m)
            return AuctionState.Acceptance;
        return AuctionState.Indeterminate;
    }

    private static LiquidityEnvironment ClassifyLiquidity(MarketIntelligenceInput input)
    {
        if (input.LiquidityQuality <= 0.2m)
            return LiquidityEnvironment.Vacuum;
        if (input.LiquidityQuality < 0.4m)
            return LiquidityEnvironment.Thin;
        if (input.AbsorptionQuality >= 0.75m)
            return LiquidityEnvironment.Absorption;
        if (input.LiquidityQuality >= 0.8m)
            return LiquidityEnvironment.Institutional;
        return LiquidityEnvironment.Normal;
    }

    private static VolatilityRegime ClassifyVolatility(decimal value)
    {
        if (value >= 0.9m) return VolatilityRegime.Extreme;
        if (value >= 0.7m) return VolatilityRegime.Expanding;
        if (value >= 0.4m) return VolatilityRegime.Normal;
        if (value >= 0.2m) return VolatilityRegime.Contracting;
        return VolatilityRegime.Compression;
    }

    private static InstitutionalBias ClassifyInstitutionalBias(MarketIntelligenceInput input)
    {
        if (input.OrderFlowBias >= 0.6m && input.LiquidityQuality >= 0.6m)
            return InstitutionalBias.Buying;
        if (input.OrderFlowBias <= -0.6m && input.LiquidityQuality >= 0.6m)
            return InstitutionalBias.Selling;
        if (input.OrderFlowBias >= 0.25m && input.AbsorptionQuality >= 0.65m)
            return InstitutionalBias.Accumulation;
        if (input.OrderFlowBias <= -0.25m && input.AbsorptionQuality >= 0.65m)
            return InstitutionalBias.Distribution;
        return InstitutionalBias.Neutral;
    }

    private static decimal CalculateQuality(MarketIntelligenceInput input, VolatilityRegime volatility, LiquidityEnvironment liquidity)
    {
        var score =
            input.DirectionalStrength * 15m +
            input.TrendPersistence * 15m +
            Math.Max(input.BalanceQuality, input.BreakoutQuality) * 10m +
            input.LiquidityQuality * 20m +
            input.HigherTimeframeAlignment * 15m +
            input.SessionQuality * 15m +
            (1m - Math.Abs(input.VolatilityLevel - 0.6m)) * 10m;

        if (volatility == VolatilityRegime.Extreme)
            score -= 15m;
        if (liquidity == LiquidityEnvironment.Thin || liquidity == LiquidityEnvironment.Vacuum)
            score -= 20m;

        return Math.Round(Math.Max(0m, Math.Min(100m, score)), 2, MidpointRounding.AwayFromZero);
    }

    private static MarketHealth ClassifyHealth(decimal score, VolatilityRegime volatility, LiquidityEnvironment liquidity)
    {
        if (liquidity == LiquidityEnvironment.Vacuum || (volatility == VolatilityRegime.Extreme && score < 60m))
            return MarketHealth.AvoidTrading;
        if (score >= 85m) return MarketHealth.Excellent;
        if (score >= 70m) return MarketHealth.Good;
        if (score >= 55m) return MarketHealth.Fair;
        if (score >= 40m) return MarketHealth.Poor;
        return MarketHealth.AvoidTrading;
    }

    private static RecommendedEnvironment RecommendEnvironment(
        MarketRegime regime,
        MarketHealth health,
        VolatilityRegime volatility,
        LiquidityEnvironment liquidity)
    {
        if (health == MarketHealth.AvoidTrading || liquidity == LiquidityEnvironment.Vacuum)
            return RecommendedEnvironment.StandAside;
        if (volatility == VolatilityRegime.Extreme)
            return RecommendedEnvironment.Scalping;
        if (regime == MarketRegime.TrendExpansion || regime == MarketRegime.TrendPullback)
            return RecommendedEnvironment.TrendFollowing;
        if (regime == MarketRegime.Breakout)
            return RecommendedEnvironment.Breakout;
        if (regime == MarketRegime.BalancedAuction || regime == MarketRegime.Rotational || regime == MarketRegime.RangeBound)
            return RecommendedEnvironment.MeanReversion;
        if (regime == MarketRegime.Reversal || regime == MarketRegime.BreakoutFailure)
            return RecommendedEnvironment.Momentum;
        return RecommendedEnvironment.StandAside;
    }

    private static decimal CalculateRiskModifier(MarketHealth health, VolatilityRegime volatility, LiquidityEnvironment liquidity)
    {
        if (health == MarketHealth.AvoidTrading) return 0m;
        var modifier = health == MarketHealth.Excellent ? 1m : health == MarketHealth.Good ? 0.8m : health == MarketHealth.Fair ? 0.6m : 0.4m;
        if (volatility == VolatilityRegime.Extreme) modifier = Math.Min(modifier, 0.5m);
        if (liquidity == LiquidityEnvironment.Thin) modifier = Math.Min(modifier, 0.5m);
        return modifier;
    }

    private static IReadOnlyList<string> BuildReasons(
        MarketRegime regime,
        AuctionState auction,
        LiquidityEnvironment liquidity,
        VolatilityRegime volatility,
        InstitutionalBias institutionalBias,
        MarketHealth health,
        RecommendedEnvironment environment,
        decimal score)
    {
        return new[]
        {
            $"Market regime is {regime}.",
            $"Auction state is {auction}.",
            $"Liquidity environment is {liquidity}.",
            $"Volatility regime is {volatility}.",
            $"Institutional bias is {institutionalBias}.",
            $"Market health is {health} with a quality score of {score:0.##}.",
            $"Recommended environment is {environment}."
        };
    }
}
