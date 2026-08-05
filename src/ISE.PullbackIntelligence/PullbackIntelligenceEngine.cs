using System;
using System.Collections.Generic;
using ISE.MarketNarrative;
using ISE.MarketOpen;
using ISE.ORBIntelligence;

namespace ISE.PullbackIntelligence;

public enum PullbackState
{
    None,
    Healthy,
    DeepHealthy,
    Retest,
    WeakRecovery,
    TrendFailure,
    Reversal,
    StandAside
}

public sealed class PullbackInput
{
    public PullbackInput(
        MarketOpenPhase marketOpenPhase,
        NarrativeBias narrativeBias,
        NarrativePhase narrativePhase,
        int narrativeStrength,
        ORBDirection orbDirection,
        decimal pullbackDepthPercent,
        decimal recoveryScore,
        decimal structureScore,
        decimal orderFlowScore,
        decimal liquidityScore,
        bool touchedOrbBreakoutLevel,
        bool structureBroken,
        bool opposingInstitutionalFlow,
        bool continuationConfirmed,
        bool positionOpen,
        bool authoritativeRiskBlock = false)
    {
        if (narrativeStrength < 0 || narrativeStrength > 100)
            throw new ArgumentOutOfRangeException(nameof(narrativeStrength));
        ValidateScore(recoveryScore, nameof(recoveryScore));
        ValidateScore(structureScore, nameof(structureScore));
        ValidateScore(orderFlowScore, nameof(orderFlowScore));
        ValidateScore(liquidityScore, nameof(liquidityScore));
        if (pullbackDepthPercent < 0m || pullbackDepthPercent > 100m)
            throw new ArgumentOutOfRangeException(nameof(pullbackDepthPercent));

        MarketOpenPhase = marketOpenPhase;
        NarrativeBias = narrativeBias;
        NarrativePhase = narrativePhase;
        NarrativeStrength = narrativeStrength;
        OrbDirection = orbDirection;
        PullbackDepthPercent = pullbackDepthPercent;
        RecoveryScore = recoveryScore;
        StructureScore = structureScore;
        OrderFlowScore = orderFlowScore;
        LiquidityScore = liquidityScore;
        TouchedOrbBreakoutLevel = touchedOrbBreakoutLevel;
        StructureBroken = structureBroken;
        OpposingInstitutionalFlow = opposingInstitutionalFlow;
        ContinuationConfirmed = continuationConfirmed;
        PositionOpen = positionOpen;
        AuthoritativeRiskBlock = authoritativeRiskBlock;
    }

    public MarketOpenPhase MarketOpenPhase { get; }
    public NarrativeBias NarrativeBias { get; }
    public NarrativePhase NarrativePhase { get; }
    public int NarrativeStrength { get; }
    public ORBDirection OrbDirection { get; }
    public decimal PullbackDepthPercent { get; }
    public decimal RecoveryScore { get; }
    public decimal StructureScore { get; }
    public decimal OrderFlowScore { get; }
    public decimal LiquidityScore { get; }
    public bool TouchedOrbBreakoutLevel { get; }
    public bool StructureBroken { get; }
    public bool OpposingInstitutionalFlow { get; }
    public bool ContinuationConfirmed { get; }
    public bool PositionOpen { get; }
    public bool AuthoritativeRiskBlock { get; }

    private static void ValidateScore(decimal value, string parameterName)
    {
        if (value < 0m || value > 100m)
            throw new ArgumentOutOfRangeException(parameterName, "Scores must be between 0 and 100.");
    }
}

public sealed class PullbackDecision
{
    public PullbackDecision(PullbackState state, int confidence, bool entryPermitted,
        bool addToWinner, bool runnerStillValid, bool exitImmediately,
        IReadOnlyList<string> reasons)
    {
        if (confidence < 0 || confidence > 100)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        State = state;
        Confidence = confidence;
        EntryPermitted = entryPermitted;
        AddToWinner = addToWinner;
        RunnerStillValid = runnerStillValid;
        ExitImmediately = exitImmediately;
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    public PullbackState State { get; }
    public int Confidence { get; }
    public bool EntryPermitted { get; }
    public bool AddToWinner { get; }
    public bool RunnerStillValid { get; }
    public bool ExitImmediately { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class PullbackIntelligenceEngine
{
    public PullbackDecision Evaluate(PullbackInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var reasons = new List<string>();

        if (input.AuthoritativeRiskBlock)
        {
            reasons.Add("Authoritative risk control overrides pullback participation.");
            return Decision(PullbackState.StandAside, 0, false, false, false,
                input.PositionOpen, reasons);
        }

        if (input.OpposingInstitutionalFlow)
        {
            reasons.Add("Opposing institutional flow invalidates the directional pullback thesis.");
            return Decision(PullbackState.Reversal, 90, false, false, false,
                input.PositionOpen, reasons);
        }

        if (input.StructureBroken || input.StructureScore < 35m)
        {
            reasons.Add("The directional market structure has failed.");
            return Decision(PullbackState.TrendFailure, 85, false, false, false,
                input.PositionOpen, reasons);
        }

        if (!DirectionalNarrative(input))
        {
            reasons.Add("No directional narrative supports pullback participation.");
            return Decision(PullbackState.StandAside, 25, false, false, false, false, reasons);
        }

        int confidence = CalculateConfidence(input);

        if (input.TouchedOrbBreakoutLevel && input.RecoveryScore >= 65m && input.StructureScore >= 60m)
        {
            reasons.Add("The pullback tested the ORB breakout level and recovered with structure intact.");
            return Decision(PullbackState.Retest, confidence, true,
                input.PositionOpen && confidence >= 88, confidence >= 78, false, reasons);
        }

        if (input.RecoveryScore < 50m || input.OrderFlowScore < 45m)
        {
            reasons.Add("The pullback recovery lacks sufficient order-flow confirmation.");
            return Decision(PullbackState.WeakRecovery, Math.Min(confidence, 50),
                false, false, false, false, reasons);
        }

        if (input.PullbackDepthPercent <= 38m && confidence >= 65)
        {
            reasons.Add("A shallow retracement is holding within a strong directional narrative.");
            return Decision(PullbackState.Healthy, confidence, true,
                input.PositionOpen && confidence >= 90, confidence >= 75, false, reasons);
        }

        if (input.PullbackDepthPercent <= 62m && input.RecoveryScore >= 72m && confidence >= 65)
        {
            reasons.Add("A deeper retracement recovered while structure and liquidity remained supportive.");
            return Decision(PullbackState.DeepHealthy, confidence, true,
                false, confidence >= 78, false, reasons);
        }

        if (input.ContinuationConfirmed && confidence >= 75)
        {
            reasons.Add("Continuation evidence confirms that the pullback has completed.");
            return Decision(PullbackState.Healthy, confidence, true,
                input.PositionOpen && confidence >= 88, true, false, reasons);
        }

        reasons.Add("The retracement is not sufficiently qualified for participation.");
        return Decision(PullbackState.StandAside, Math.Min(confidence, 55),
            false, false, false, false, reasons);
    }

    private static bool DirectionalNarrative(PullbackInput input)
    {
        bool directionAligned =
            input.NarrativeBias == NarrativeBias.Bullish && input.OrbDirection == ORBDirection.Long ||
            input.NarrativeBias == NarrativeBias.Bearish && input.OrbDirection == ORBDirection.Short;

        bool validPhase = input.NarrativePhase == NarrativePhase.Expansion ||
                          input.NarrativePhase == NarrativePhase.Pullback ||
                          input.NarrativePhase == NarrativePhase.Continuation;

        return directionAligned && validPhase && input.NarrativeStrength >= 55;
    }

    private static int CalculateConfidence(PullbackInput input)
    {
        decimal depthQuality = input.PullbackDepthPercent <= 38m ? 90m :
                               input.PullbackDepthPercent <= 62m ? 70m : 35m;
        decimal phaseAdjustment = input.MarketOpenPhase == MarketOpenPhase.PullbackWindow ? 5m : 0m;
        decimal continuationAdjustment = input.ContinuationConfirmed ? 7m : 0m;

        decimal raw =
            input.NarrativeStrength * 0.20m +
            input.RecoveryScore * 0.25m +
            input.StructureScore * 0.20m +
            input.OrderFlowScore * 0.15m +
            input.LiquidityScore * 0.10m +
            depthQuality * 0.10m +
            phaseAdjustment +
            continuationAdjustment;

        return (int)Math.Max(0m, Math.Min(100m,
            Math.Round(raw, 0, MidpointRounding.AwayFromZero)));
    }

    private static PullbackDecision Decision(PullbackState state, int confidence,
        bool entryPermitted, bool addToWinner, bool runnerStillValid,
        bool exitImmediately, IReadOnlyList<string> reasons)
        => new PullbackDecision(state, confidence, entryPermitted, addToWinner,
            runnerStillValid, exitImmediately, reasons);
}
