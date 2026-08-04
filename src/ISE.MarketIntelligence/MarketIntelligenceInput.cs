using System;

namespace ISE.MarketIntelligence;

/// <summary>Provides normalized evidence from existing market-analysis engines.</summary>
public sealed class MarketIntelligenceInput
{
    /// <summary>Initializes a market-intelligence request.</summary>
    public MarketIntelligenceInput(
        decimal directionalStrength,
        decimal trendPersistence,
        decimal balanceQuality,
        decimal breakoutQuality,
        decimal rejectionQuality,
        decimal liquidityQuality,
        decimal absorptionQuality,
        decimal volatilityLevel,
        decimal orderFlowBias,
        decimal higherTimeframeAlignment,
        decimal sessionQuality)
    {
        DirectionalStrength = ValidateUnit(directionalStrength, nameof(directionalStrength));
        TrendPersistence = ValidateUnit(trendPersistence, nameof(trendPersistence));
        BalanceQuality = ValidateUnit(balanceQuality, nameof(balanceQuality));
        BreakoutQuality = ValidateUnit(breakoutQuality, nameof(breakoutQuality));
        RejectionQuality = ValidateUnit(rejectionQuality, nameof(rejectionQuality));
        LiquidityQuality = ValidateUnit(liquidityQuality, nameof(liquidityQuality));
        AbsorptionQuality = ValidateUnit(absorptionQuality, nameof(absorptionQuality));
        VolatilityLevel = ValidateUnit(volatilityLevel, nameof(volatilityLevel));

        if (orderFlowBias < -1m || orderFlowBias > 1m)
            throw new ArgumentOutOfRangeException(nameof(orderFlowBias), "Order-flow bias must be between minus one and one.");

        OrderFlowBias = orderFlowBias;
        HigherTimeframeAlignment = ValidateUnit(higherTimeframeAlignment, nameof(higherTimeframeAlignment));
        SessionQuality = ValidateUnit(sessionQuality, nameof(sessionQuality));
    }

    /// <summary>Gets directional strength.</summary>
    public decimal DirectionalStrength { get; }
    /// <summary>Gets trend persistence.</summary>
    public decimal TrendPersistence { get; }
    /// <summary>Gets balance quality.</summary>
    public decimal BalanceQuality { get; }
    /// <summary>Gets breakout quality.</summary>
    public decimal BreakoutQuality { get; }
    /// <summary>Gets rejection quality.</summary>
    public decimal RejectionQuality { get; }
    /// <summary>Gets liquidity quality.</summary>
    public decimal LiquidityQuality { get; }
    /// <summary>Gets absorption quality.</summary>
    public decimal AbsorptionQuality { get; }
    /// <summary>Gets normalized volatility level.</summary>
    public decimal VolatilityLevel { get; }
    /// <summary>Gets signed order-flow bias from minus one to one.</summary>
    public decimal OrderFlowBias { get; }
    /// <summary>Gets higher-timeframe alignment.</summary>
    public decimal HigherTimeframeAlignment { get; }
    /// <summary>Gets session quality.</summary>
    public decimal SessionQuality { get; }

    private static decimal ValidateUnit(decimal value, string name)
    {
        if (value < 0m || value > 1m)
            throw new ArgumentOutOfRangeException(name, "Value must be between zero and one.");
        return value;
    }
}
