using System;
using System.Collections.Generic;
using ISE.RunnerIntelligence;

namespace ISE.TradeSupervisor;

public enum TradeSupervisorState
{
    Hold,
    Reduce,
    Add,
    Protect,
    TightenStop,
    PromoteRunner,
    Exit,
    ForceExit
}

public sealed class TradeSupervisorInput
{
    public TradeSupervisorInput(
        RunnerState runnerState,
        RunnerAction runnerAction,
        int trendPersistenceScore,
        bool thesisStillValid,
        bool institutionalReversal,
        bool structureWeakening,
        decimal openProfitR,
        decimal riskPressureScore,
        int minutesUntilForceFlat,
        bool positionOpen = true,
        bool authoritativeRiskBlock = false)
    {
        if (trendPersistenceScore < 0 || trendPersistenceScore > 100)
            throw new ArgumentOutOfRangeException(nameof(trendPersistenceScore));
        if (riskPressureScore < 0m || riskPressureScore > 100m)
            throw new ArgumentOutOfRangeException(nameof(riskPressureScore));
        if (minutesUntilForceFlat < 0)
            throw new ArgumentOutOfRangeException(nameof(minutesUntilForceFlat));

        RunnerState = runnerState;
        RunnerAction = runnerAction;
        TrendPersistenceScore = trendPersistenceScore;
        ThesisStillValid = thesisStillValid;
        InstitutionalReversal = institutionalReversal;
        StructureWeakening = structureWeakening;
        OpenProfitR = openProfitR;
        RiskPressureScore = riskPressureScore;
        MinutesUntilForceFlat = minutesUntilForceFlat;
        PositionOpen = positionOpen;
        AuthoritativeRiskBlock = authoritativeRiskBlock;
    }

    public RunnerState RunnerState { get; }
    public RunnerAction RunnerAction { get; }
    public int TrendPersistenceScore { get; }
    public bool ThesisStillValid { get; }
    public bool InstitutionalReversal { get; }
    public bool StructureWeakening { get; }
    public decimal OpenProfitR { get; }
    public decimal RiskPressureScore { get; }
    public int MinutesUntilForceFlat { get; }
    public bool PositionOpen { get; }
    public bool AuthoritativeRiskBlock { get; }
}

public sealed class TradeSupervisorDecision
{
    public TradeSupervisorDecision(
        TradeSupervisorState state,
        int confidence,
        bool thesisStillValid,
        bool runnerStillValid,
        bool canScale,
        bool tightenStops,
        bool exitImmediately,
        IReadOnlyList<string> reasons)
    {
        if (confidence < 0 || confidence > 100)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        State = state;
        Confidence = confidence;
        ThesisStillValid = thesisStillValid;
        RunnerStillValid = runnerStillValid;
        CanScale = canScale;
        TightenStops = tightenStops;
        ExitImmediately = exitImmediately;
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    public TradeSupervisorState State { get; }
    public int Confidence { get; }
    public bool ThesisStillValid { get; }
    public bool RunnerStillValid { get; }
    public bool CanScale { get; }
    public bool TightenStops { get; }
    public bool ExitImmediately { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class TradeSupervisorEngine
{
    private const int ForceFlatBufferMinutes = 5;

    public TradeSupervisorDecision Evaluate(TradeSupervisorInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var reasons = new List<string>();

        if (!input.PositionOpen)
        {
            reasons.Add("No open position requires supervision.");
            return Decision(TradeSupervisorState.Hold, 0, input.ThesisStillValid,
                false, false, false, false, reasons);
        }

        if (input.MinutesUntilForceFlat <= ForceFlatBufferMinutes)
        {
            reasons.Add("The authoritative end-of-day force-flat window has been reached.");
            return Decision(TradeSupervisorState.ForceExit, 100, false,
                false, false, true, true, reasons);
        }

        if (input.AuthoritativeRiskBlock)
        {
            reasons.Add("Authoritative risk control requires immediate exit.");
            return Decision(TradeSupervisorState.Exit, 100, false,
                false, false, true, true, reasons);
        }

        if (input.InstitutionalReversal || !input.ThesisStillValid ||
            input.RunnerState == RunnerState.Reversal)
        {
            reasons.Add("The original trade thesis has been invalidated.");
            return Decision(TradeSupervisorState.Exit, 95, false,
                false, false, true, true, reasons);
        }

        if (input.StructureWeakening || input.RunnerState == RunnerState.Exhaustion ||
            input.RunnerAction == RunnerAction.Reduce)
        {
            reasons.Add("Weakening structure requires partial risk reduction.");
            return Decision(TradeSupervisorState.Reduce, 80, true,
                false, false, true, false, reasons);
        }

        if (input.RiskPressureScore >= 70m)
        {
            reasons.Add("Rising risk pressure requires protecting the position.");
            return Decision(TradeSupervisorState.Protect, 78, true,
                input.TrendPersistenceScore >= 75, false, true, false, reasons);
        }

        if (input.OpenProfitR >= 2m && input.TrendPersistenceScore < 78)
        {
            reasons.Add("Open profit is substantial while persistence has weakened; tighten the stop.");
            return Decision(TradeSupervisorState.TightenStop, 76, true,
                false, false, true, false, reasons);
        }

        if (input.RunnerState == RunnerState.EliteRunner &&
            input.RunnerAction == RunnerAction.Promote &&
            input.TrendPersistenceScore >= 90)
        {
            reasons.Add("Elite persistence supports runner promotion and qualified scaling.");
            return Decision(TradeSupervisorState.PromoteRunner, 95, true,
                true, true, false, false, reasons);
        }

        if (input.RunnerState == RunnerState.ConfirmedRunner &&
            input.RunnerAction == RunnerAction.Hold &&
            input.TrendPersistenceScore >= 78)
        {
            reasons.Add("The runner thesis remains healthy and should be held.");
            return Decision(TradeSupervisorState.Hold, input.TrendPersistenceScore, true,
                true, false, false, false, reasons);
        }

        reasons.Add("The position remains valid but requires protective management.");
        return Decision(TradeSupervisorState.Protect, Math.Max(50, input.TrendPersistenceScore),
            true, input.TrendPersistenceScore >= 70, false, true, false, reasons);
    }

    private static TradeSupervisorDecision Decision(
        TradeSupervisorState state,
        int confidence,
        bool thesisValid,
        bool runnerValid,
        bool canScale,
        bool tighten,
        bool exit,
        IReadOnlyList<string> reasons)
        => new TradeSupervisorDecision(state, confidence, thesisValid, runnerValid,
            canScale, tighten, exit, reasons);
}
