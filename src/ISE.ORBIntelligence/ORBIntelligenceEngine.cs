using System;
using System.Collections.Generic;
using ISE.MarketOpen;

namespace ISE.ORBIntelligence;

public enum ORBState
{
    Forming,
    AwaitBreakout,
    BreakoutPending,
    BreakoutConfirmed,
    BreakoutRejected,
    LiquiditySweep,
    RetestQualified,
    RetestFailed,
    StandAside
}

public enum ORBDirection
{
    None,
    Long,
    Short
}

public sealed class ORBInput
{
    public ORBInput(
        MarketOpenPhase marketOpenPhase,
        bool openingRangeComplete,
        decimal openingRangeHigh,
        decimal openingRangeLow,
        decimal currentPrice,
        decimal breakoutDistanceTicks,
        decimal volumeRatio,
        decimal orderFlowScore,
        decimal liquidityScore,
        decimal structureAlignment,
        bool returnedInsideRange,
        bool retestAttempted,
        bool retestHeld,
        bool authoritativeRiskBlock = false)
    {
        if (openingRangeHigh < openingRangeLow)
            throw new ArgumentException("Opening-range high cannot be below opening-range low.");
        ValidateScore(orderFlowScore, nameof(orderFlowScore));
        ValidateScore(liquidityScore, nameof(liquidityScore));
        ValidateScore(structureAlignment, nameof(structureAlignment));
        if (volumeRatio < 0m) throw new ArgumentOutOfRangeException(nameof(volumeRatio));
        if (breakoutDistanceTicks < 0m) throw new ArgumentOutOfRangeException(nameof(breakoutDistanceTicks));

        MarketOpenPhase = marketOpenPhase;
        OpeningRangeComplete = openingRangeComplete;
        OpeningRangeHigh = openingRangeHigh;
        OpeningRangeLow = openingRangeLow;
        CurrentPrice = currentPrice;
        BreakoutDistanceTicks = breakoutDistanceTicks;
        VolumeRatio = volumeRatio;
        OrderFlowScore = orderFlowScore;
        LiquidityScore = liquidityScore;
        StructureAlignment = structureAlignment;
        ReturnedInsideRange = returnedInsideRange;
        RetestAttempted = retestAttempted;
        RetestHeld = retestHeld;
        AuthoritativeRiskBlock = authoritativeRiskBlock;
    }

    public MarketOpenPhase MarketOpenPhase { get; }
    public bool OpeningRangeComplete { get; }
    public decimal OpeningRangeHigh { get; }
    public decimal OpeningRangeLow { get; }
    public decimal CurrentPrice { get; }
    public decimal BreakoutDistanceTicks { get; }
    public decimal VolumeRatio { get; }
    public decimal OrderFlowScore { get; }
    public decimal LiquidityScore { get; }
    public decimal StructureAlignment { get; }
    public bool ReturnedInsideRange { get; }
    public bool RetestAttempted { get; }
    public bool RetestHeld { get; }
    public bool AuthoritativeRiskBlock { get; }

    private static void ValidateScore(decimal value, string parameterName)
    {
        if (value < 0m || value > 100m)
            throw new ArgumentOutOfRangeException(parameterName, "Scores must be between 0 and 100.");
    }
}

public sealed class ORBDecision
{
    public ORBDecision(ORBState state, ORBDirection direction, int confidence,
        bool entryPermitted, bool waitForRetest, bool runnerCandidate,
        IReadOnlyList<string> reasons)
    {
        if (confidence < 0 || confidence > 100)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        State = state;
        Direction = direction;
        Confidence = confidence;
        EntryPermitted = entryPermitted;
        WaitForRetest = waitForRetest;
        RunnerCandidate = runnerCandidate;
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    public ORBState State { get; }
    public ORBDirection Direction { get; }
    public int Confidence { get; }
    public bool EntryPermitted { get; }
    public bool WaitForRetest { get; }
    public bool RunnerCandidate { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class ORBIntelligenceEngine
{
    public ORBDecision Evaluate(ORBInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var reasons = new List<string>();

        if (input.AuthoritativeRiskBlock)
        {
            reasons.Add("Authoritative risk control blocks ORB participation.");
            return Decision(ORBState.StandAside, ORBDirection.None, 0, false, false, false, reasons);
        }

        if (!input.OpeningRangeComplete)
        {
            reasons.Add("The opening range is still forming.");
            return Decision(ORBState.Forming, ORBDirection.None, 0, false, false, false, reasons);
        }

        var direction = ResolveDirection(input);
        int confidence = CalculateConfidence(input);

        if (input.ReturnedInsideRange)
        {
            reasons.Add("Price broke the range and then returned inside it.");
            bool sweep = input.BreakoutDistanceTicks >= 4m;
            return Decision(sweep ? ORBState.LiquiditySweep : ORBState.BreakoutRejected,
                direction, Math.Min(confidence, 45), false, false, false, reasons);
        }

        if (direction == ORBDirection.None)
        {
            reasons.Add("Price remains inside the completed opening range.");
            return Decision(ORBState.AwaitBreakout, direction, 20, false, false, false, reasons);
        }

        if (input.RetestAttempted)
        {
            if (input.RetestHeld && confidence >= 65)
            {
                reasons.Add("The breakout retest held with aligned evidence.");
                return Decision(ORBState.RetestQualified, direction, confidence, true, false,
                    confidence >= 85, reasons);
            }

            reasons.Add("The breakout retest failed or lacked sufficient confirmation.");
            return Decision(ORBState.RetestFailed, direction, Math.Min(confidence, 50), false, false, false, reasons);
        }

        if (input.BreakoutDistanceTicks < 2m)
        {
            reasons.Add("Price is outside the range, but the breakout has not separated sufficiently.");
            return Decision(ORBState.BreakoutPending, direction, confidence, false, true, false, reasons);
        }

        if (confidence >= 75)
        {
            reasons.Add("Volume, order flow, liquidity, and structure support breakout acceptance.");
            return Decision(ORBState.BreakoutConfirmed, direction, confidence, true,
                confidence < 85, confidence >= 88, reasons);
        }

        reasons.Add("The breakout lacks enough aligned evidence for immediate participation.");
        return Decision(ORBState.BreakoutRejected, direction, confidence, false, true, false, reasons);
    }

    private static ORBDirection ResolveDirection(ORBInput input)
    {
        if (input.CurrentPrice > input.OpeningRangeHigh) return ORBDirection.Long;
        if (input.CurrentPrice < input.OpeningRangeLow) return ORBDirection.Short;
        return ORBDirection.None;
    }

    private static int CalculateConfidence(ORBInput input)
    {
        decimal volumeScore = Math.Min(100m, input.VolumeRatio * 50m);
        decimal distanceScore = Math.Min(100m, input.BreakoutDistanceTicks * 10m);
        decimal phaseAdjustment = input.MarketOpenPhase == MarketOpenPhase.ReversalWindow ? -5m : 0m;

        decimal raw =
            volumeScore * 0.20m +
            input.OrderFlowScore * 0.25m +
            input.LiquidityScore * 0.20m +
            input.StructureAlignment * 0.25m +
            distanceScore * 0.10m +
            phaseAdjustment;

        return (int)Math.Max(0m, Math.Min(100m,
            Math.Round(raw, 0, MidpointRounding.AwayFromZero)));
    }

    private static ORBDecision Decision(ORBState state, ORBDirection direction, int confidence,
        bool entryPermitted, bool waitForRetest, bool runnerCandidate, IReadOnlyList<string> reasons)
        => new ORBDecision(state, direction, confidence, entryPermitted,
            waitForRetest, runnerCandidate, reasons);
}
