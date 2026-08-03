using System;
using System.Collections.Generic;
using ISE.MarketStructure;
using ISE.OrderFlow;
using ISE.Trend;

namespace ISE.Signal;

/// <summary>Combines normalized analytical evidence into an explainable trade candidate.</summary>
public sealed class SignalEngine
{
    private const int TrendWeight = 30;
    private const int StructureWeight = 25;
    private const int LiquidityWeight = 25;
    private const int OrderFlowWeight = 20;

    /// <summary>Evaluates the supplied evidence and returns a deterministic signal decision.</summary>
    public SignalSnapshot Evaluate(SignalInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var longScore = 0;
        var shortScore = 0;
        var longReasons = new List<SignalReason>();
        var shortReasons = new List<SignalReason>();

        ScoreTrend(input.TrendDirection, ref longScore, ref shortScore, longReasons, shortReasons);
        ScoreStructure(input.StructureDirection, ref longScore, ref shortScore, longReasons, shortReasons);
        ScoreLiquidity(input, ref longScore, ref shortScore, longReasons, shortReasons);
        ScoreOrderFlow(input.OrderFlowBias, ref longScore, ref shortScore, longReasons, shortReasons);

        if (!input.TradingPermitted)
        {
            return new SignalSnapshot(
                SignalDirection.None,
                Math.Max(longScore, shortScore),
                false,
                new[] { SignalReason.TradingBlocked });
        }

        if (longScore == shortScore)
        {
            return new SignalSnapshot(
                SignalDirection.None,
                longScore,
                false,
                new[] { SignalReason.TradingPermitted, SignalReason.ConflictingEvidence });
        }

        var direction = longScore > shortScore ? SignalDirection.Long : SignalDirection.Short;
        var confidence = Math.Max(longScore, shortScore);
        var reasons = longScore > shortScore ? longReasons : shortReasons;
        reasons.Insert(0, SignalReason.TradingPermitted);

        if (confidence < input.MinimumConfidence)
        {
            reasons.Add(SignalReason.ConfidenceBelowThreshold);
            return new SignalSnapshot(SignalDirection.None, confidence, false, reasons.ToArray());
        }

        return new SignalSnapshot(direction, confidence, true, reasons.ToArray());
    }

    private static void ScoreTrend(
        TrendDirection direction,
        ref int longScore,
        ref int shortScore,
        ICollection<SignalReason> longReasons,
        ICollection<SignalReason> shortReasons)
    {
        if (direction == TrendDirection.Bullish)
        {
            longScore += TrendWeight;
            longReasons.Add(SignalReason.BullishTrend);
        }
        else if (direction == TrendDirection.Bearish)
        {
            shortScore += TrendWeight;
            shortReasons.Add(SignalReason.BearishTrend);
        }
    }

    private static void ScoreStructure(
        StructureDirection direction,
        ref int longScore,
        ref int shortScore,
        ICollection<SignalReason> longReasons,
        ICollection<SignalReason> shortReasons)
    {
        if (direction == StructureDirection.Bullish)
        {
            longScore += StructureWeight;
            longReasons.Add(SignalReason.BullishStructure);
        }
        else if (direction == StructureDirection.Bearish)
        {
            shortScore += StructureWeight;
            shortReasons.Add(SignalReason.BearishStructure);
        }
    }

    private static void ScoreLiquidity(
        SignalInput input,
        ref int longScore,
        ref int shortScore,
        ICollection<SignalReason> longReasons,
        ICollection<SignalReason> shortReasons)
    {
        if (input.SellSideLiquidityReclaimed)
        {
            longScore += LiquidityWeight;
            longReasons.Add(SignalReason.SellSideLiquidityReclaimed);
        }

        if (input.BuySideLiquidityReclaimed)
        {
            shortScore += LiquidityWeight;
            shortReasons.Add(SignalReason.BuySideLiquidityReclaimed);
        }
    }

    private static void ScoreOrderFlow(
        OrderFlowBias bias,
        ref int longScore,
        ref int shortScore,
        ICollection<SignalReason> longReasons,
        ICollection<SignalReason> shortReasons)
    {
        if (bias == OrderFlowBias.Bullish)
        {
            longScore += OrderFlowWeight;
            longReasons.Add(SignalReason.BullishOrderFlow);
        }
        else if (bias == OrderFlowBias.Bearish)
        {
            shortScore += OrderFlowWeight;
            shortReasons.Add(SignalReason.BearishOrderFlow);
        }
    }
}
