using System;

namespace ISE.MarketState;

/// <summary>Provides normalized evidence used to classify the current market regime.</summary>
public sealed class MarketStateInput
{
    /// <summary>Initializes a market-state evidence snapshot.</summary>
    public MarketStateInput(decimal directionalEfficiency, decimal trendStrength, decimal volatilityRatio, decimal rangeExpansion, decimal pullbackDepth, decimal breakoutAcceptance, decimal reversalEvidence, decimal exhaustionEvidence)
    {
        DirectionalEfficiency = ValidateSignedUnit(directionalEfficiency, nameof(directionalEfficiency));
        TrendStrength = ValidateUnit(trendStrength, nameof(trendStrength));
        VolatilityRatio = ValidateNonNegative(volatilityRatio, nameof(volatilityRatio));
        RangeExpansion = ValidateUnit(rangeExpansion, nameof(rangeExpansion));
        PullbackDepth = ValidateUnit(pullbackDepth, nameof(pullbackDepth));
        BreakoutAcceptance = ValidateUnit(breakoutAcceptance, nameof(breakoutAcceptance));
        ReversalEvidence = ValidateUnit(reversalEvidence, nameof(reversalEvidence));
        ExhaustionEvidence = ValidateUnit(exhaustionEvidence, nameof(exhaustionEvidence));
    }

    /// <summary>Gets signed directional efficiency from -1 for bearish to +1 for bullish.</summary>
    public decimal DirectionalEfficiency { get; }
    /// <summary>Gets normalized trend persistence from zero to one.</summary>
    public decimal TrendStrength { get; }
    /// <summary>Gets current volatility divided by its baseline.</summary>
    public decimal VolatilityRatio { get; }
    /// <summary>Gets normalized evidence of expanding price range.</summary>
    public decimal RangeExpansion { get; }
    /// <summary>Gets normalized retracement depth relative to the active impulse.</summary>
    public decimal PullbackDepth { get; }
    /// <summary>Gets normalized acceptance beyond the prior balance boundary.</summary>
    public decimal BreakoutAcceptance { get; }
    /// <summary>Gets normalized evidence that directional control has reversed.</summary>
    public decimal ReversalEvidence { get; }
    /// <summary>Gets normalized evidence that an extended move is exhausting.</summary>
    public decimal ExhaustionEvidence { get; }

    private static decimal ValidateSignedUnit(decimal value, string name)
    {
        if (value < -1m || value > 1m)
            throw new ArgumentOutOfRangeException(name, "Value must be between -1 and 1.");
        return value;
    }

    private static decimal ValidateUnit(decimal value, string name)
    {
        if (value < 0m || value > 1m)
            throw new ArgumentOutOfRangeException(name, "Value must be between 0 and 1.");
        return value;
    }

    private static decimal ValidateNonNegative(decimal value, string name)
    {
        if (value < 0m)
            throw new ArgumentOutOfRangeException(name, "Value cannot be negative.");
        return value;
    }
}
