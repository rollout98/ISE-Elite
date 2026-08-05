using System;
using System.Collections.Generic;
using ISE.MarketNarrative;
using ISE.PullbackIntelligence;

namespace ISE.RunnerIntelligence;

public enum RunnerState
{
    NotRunner,
    PossibleRunner,
    ConfirmedRunner,
    EliteRunner,
    Exhaustion,
    Reversal,
    StandAside
}

public enum RunnerAction
{
    Exit,
    Reduce,
    Protect,
    Hold,
    Promote
}

public sealed class RunnerInput
{
    public RunnerInput(
        NarrativeBias narrativeBias,
        NarrativePhase narrativePhase,
        int narrativeStrength,
        PullbackState pullbackState,
        int healthyPullbackCount,
        decimal structureScore,
        decimal orderFlowScore,
        decimal liquidityScore,
        decimal momentumScore,
        bool continuationConfirmed,
        bool exhaustionDetected,
        bool institutionalReversal,
        bool positionOpen = true,
        bool authoritativeRiskBlock = false)
    {
        ValidateScore(narrativeStrength, nameof(narrativeStrength));
        ValidateScore(structureScore, nameof(structureScore));
        ValidateScore(orderFlowScore, nameof(orderFlowScore));
        ValidateScore(liquidityScore, nameof(liquidityScore));
        ValidateScore(momentumScore, nameof(momentumScore));
        if (healthyPullbackCount < 0) throw new ArgumentOutOfRangeException(nameof(healthyPullbackCount));

        NarrativeBias = narrativeBias;
        NarrativePhase = narrativePhase;
        NarrativeStrength = narrativeStrength;
        PullbackState = pullbackState;
        HealthyPullbackCount = healthyPullbackCount;
        StructureScore = structureScore;
        OrderFlowScore = orderFlowScore;
        LiquidityScore = liquidityScore;
        MomentumScore = momentumScore;
        ContinuationConfirmed = continuationConfirmed;
        ExhaustionDetected = exhaustionDetected;
        InstitutionalReversal = institutionalReversal;
        PositionOpen = positionOpen;
        AuthoritativeRiskBlock = authoritativeRiskBlock;
    }

    public NarrativeBias NarrativeBias { get; }
    public NarrativePhase NarrativePhase { get; }
    public int NarrativeStrength { get; }
    public PullbackState PullbackState { get; }
    public int HealthyPullbackCount { get; }
    public decimal StructureScore { get; }
    public decimal OrderFlowScore { get; }
    public decimal LiquidityScore { get; }
    public decimal MomentumScore { get; }
    public bool ContinuationConfirmed { get; }
    public bool ExhaustionDetected { get; }
    public bool InstitutionalReversal { get; }
    public bool PositionOpen { get; }
    public bool AuthoritativeRiskBlock { get; }

    private static void ValidateScore(decimal value, string parameterName)
    {
        if (value < 0m || value > 100m)
            throw new ArgumentOutOfRangeException(parameterName, "Scores must be between 0 and 100.");
    }
}

public sealed class RunnerDecision
{
    public RunnerDecision(RunnerState state, RunnerAction action, int trendPersistenceScore,
        bool holdPosition, bool allowScaleIn, bool tightenStop, bool exitImmediately,
        IReadOnlyList<string> reasons)
    {
        if (trendPersistenceScore < 0 || trendPersistenceScore > 100)
            throw new ArgumentOutOfRangeException(nameof(trendPersistenceScore));

        State = state;
        Action = action;
        TrendPersistenceScore = trendPersistenceScore;
        HoldPosition = holdPosition;
        AllowScaleIn = allowScaleIn;
        TightenStop = tightenStop;
        ExitImmediately = exitImmediately;
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    public RunnerState State { get; }
    public RunnerAction Action { get; }
    public int TrendPersistenceScore { get; }
    public bool HoldPosition { get; }
    public bool AllowScaleIn { get; }
    public bool TightenStop { get; }
    public bool ExitImmediately { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class RunnerIntelligenceEngine
{
    public RunnerDecision Evaluate(RunnerInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var reasons = new List<string>();

        if (input.AuthoritativeRiskBlock)
        {
            reasons.Add("Authoritative risk control overrides runner management.");
            return Decision(RunnerState.StandAside, RunnerAction.Exit, 0, false, false, true,
                input.PositionOpen, reasons);
        }

        if (input.InstitutionalReversal)
        {
            reasons.Add("Institutional reversal evidence invalidates the runner thesis.");
            return Decision(RunnerState.Reversal, RunnerAction.Exit, 0, false, false, true,
                input.PositionOpen, reasons);
        }

        var score = CalculateTrendPersistence(input);

        if (input.ExhaustionDetected)
        {
            reasons.Add("Exhaustion evidence requires protecting or reducing the position.");
            return Decision(RunnerState.Exhaustion, RunnerAction.Reduce, Math.Min(score, 45),
                false, false, true, false, reasons);
        }

        if (!input.PositionOpen || input.NarrativeBias == NarrativeBias.Neutral ||
            input.NarrativePhase == NarrativePhase.Balance || input.NarrativePhase == NarrativePhase.StandAside)
        {
            reasons.Add("No active directional runner thesis is available.");
            return Decision(RunnerState.NotRunner, RunnerAction.Protect, Math.Min(score, 39),
                false, false, false, false, reasons);
        }

        if (input.PullbackState == PullbackState.TrendFailure || input.PullbackState == PullbackState.Reversal)
        {
            reasons.Add("Pullback evidence indicates trend failure.");
            return Decision(RunnerState.Reversal, RunnerAction.Exit, Math.Min(score, 25),
                false, false, true, true, reasons);
        }

        if (score >= 90 && input.HealthyPullbackCount >= 2 && input.ContinuationConfirmed)
        {
            reasons.Add("Repeated healthy pullbacks and strong continuation confirm an elite runner.");
            return Decision(RunnerState.EliteRunner, RunnerAction.Promote, score,
                true, true, false, false, reasons);
        }

        if (score >= 78 && input.ContinuationConfirmed && IsHealthy(input.PullbackState))
        {
            reasons.Add("Directional evidence confirms runner persistence.");
            return Decision(RunnerState.ConfirmedRunner, RunnerAction.Hold, score,
                true, input.HealthyPullbackCount > 0, false, false, reasons);
        }

        if (score >= 62)
        {
            reasons.Add("Trend evidence is developing but runner confirmation is incomplete.");
            return Decision(RunnerState.PossibleRunner, RunnerAction.Protect, score,
                true, false, true, false, reasons);
        }

        reasons.Add("Trend persistence is insufficient for runner promotion.");
        return Decision(RunnerState.NotRunner, RunnerAction.Reduce, score,
            false, false, true, false, reasons);
    }

    private static int CalculateTrendPersistence(RunnerInput input)
    {
        decimal raw = input.NarrativeStrength * 0.25m +
                      input.StructureScore * 0.25m +
                      input.OrderFlowScore * 0.20m +
                      input.LiquidityScore * 0.15m +
                      input.MomentumScore * 0.15m;

        if (input.ContinuationConfirmed) raw += 6m;
        if (IsHealthy(input.PullbackState)) raw += 5m;
        if (input.HealthyPullbackCount >= 2) raw += 4m;
        if (input.PullbackState == PullbackState.WeakRecovery) raw -= 15m;
        if (input.NarrativePhase == NarrativePhase.Reversal || input.NarrativePhase == NarrativePhase.Distribution) raw -= 20m;

        return (int)Math.Max(0m, Math.Min(100m,
            Math.Round(raw, 0, MidpointRounding.AwayFromZero)));
    }

    private static bool IsHealthy(PullbackState state)
        => state == PullbackState.Healthy || state == PullbackState.DeepHealthy || state == PullbackState.Retest;

    private static RunnerDecision Decision(RunnerState state, RunnerAction action, int score,
        bool hold, bool scale, bool tighten, bool exit, IReadOnlyList<string> reasons)
        => new RunnerDecision(state, action, score, hold, scale, tighten, exit, reasons);
}
