using System;

namespace ISE.MarketState;

/// <summary>Classifies the dominant market regime from normalized structural evidence.</summary>
public sealed class MarketStateEngine
{
    /// <summary>Evaluates one market-state evidence snapshot.</summary>
    public MarketStateSnapshot Evaluate(MarketStateInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        var direction = Math.Abs(input.DirectionalEfficiency);

        if (input.ReversalEvidence >= 0.75m)
            return Result(MarketStateClassification.Reversal, input.ReversalEvidence, "Reversal evidence overrides the preceding directional regime.");

        if (input.ExhaustionEvidence >= 0.75m)
            return Result(MarketStateClassification.Exhaustion, input.ExhaustionEvidence, "Momentum deterioration indicates an exhausted directional move.");

        if (input.BreakoutAcceptance >= 0.70m && input.RangeExpansion >= 0.55m)
            return Result(MarketStateClassification.Breakout, Average(input.BreakoutAcceptance, input.RangeExpansion), "Price is accepting beyond balance with expanding range.");

        if (input.TrendStrength >= 0.60m && direction >= 0.35m && input.PullbackDepth >= 0.25m && input.PullbackDepth <= 0.70m)
            return Result(MarketStateClassification.Pullback, Average(input.TrendStrength, direction), "A controlled retracement is occurring inside an established trend.");

        if (input.TrendStrength >= 0.65m && input.DirectionalEfficiency >= 0.45m)
            return Result(MarketStateClassification.BullTrend, Average(input.TrendStrength, input.DirectionalEfficiency), "Directional efficiency and persistence confirm bullish control.");

        if (input.TrendStrength >= 0.65m && input.DirectionalEfficiency <= -0.45m)
            return Result(MarketStateClassification.BearTrend, Average(input.TrendStrength, direction), "Directional efficiency and persistence confirm bearish control.");

        if (input.VolatilityRatio >= 1.40m && input.RangeExpansion >= 0.70m)
            return Result(MarketStateClassification.Expansion, Math.Min(1m, Average(input.RangeExpansion, Math.Min(1m, input.VolatilityRatio / 2m))), "Volatility and range are expanding without sufficient trend acceptance.");

        if (input.VolatilityRatio <= 0.80m && input.RangeExpansion <= 0.35m)
            return Result(MarketStateClassification.Compression, Average(1m - input.RangeExpansion, 1m - input.VolatilityRatio), "Range and volatility are contracting.");

        if (direction <= 0.30m && input.TrendStrength <= 0.45m)
            return Result(MarketStateClassification.Rotation, 1m - Average(direction, input.TrendStrength), "Low directional efficiency indicates two-sided rotational trade.");

        return Result(MarketStateClassification.Indeterminate, 0.35m, "Available evidence does not support a reliable dominant regime.");
    }

    private static MarketStateSnapshot Result(MarketStateClassification classification, decimal confidence, string reason) =>
        new MarketStateSnapshot(classification, Math.Max(0m, Math.Min(1m, confidence)), reason);

    private static decimal Average(decimal first, decimal second) => (first + second) / 2m;
}
