using System;
using System.Collections.Generic;
using ISE.DecisionExplainability;
using ISE.MarketNarrative;
using ISE.ORBIntelligence;
using ISE.PullbackIntelligence;
using ISE.RunnerIntelligence;
using ISE.TradeSupervisor;

namespace ISE.IntegratedTradingBrainV2;

public enum BrainAction
{
    StandAside,
    EnterLong,
    EnterShort,
    Hold,
    Protect,
    Reduce,
    PromoteRunner,
    Exit,
    ForceExit
}

public sealed class IntegratedTradingBrainV2Input
{
    public IntegratedTradingBrainV2Input(
        NarrativeBias narrativeBias,
        int narrativeStrength,
        ORBState orbState,
        PullbackState pullbackState,
        RunnerState runnerState,
        TradeSupervisorState supervisorState,
        int confidence,
        bool positionOpen,
        bool entryWindowOpen,
        bool authoritativeRiskBlock = false,
        bool forceFlatWindow = false)
    {
        if (narrativeStrength < 0 || narrativeStrength > 100) throw new ArgumentOutOfRangeException(nameof(narrativeStrength));
        if (confidence < 0 || confidence > 100) throw new ArgumentOutOfRangeException(nameof(confidence));
        NarrativeBias = narrativeBias;
        NarrativeStrength = narrativeStrength;
        OrbState = orbState;
        PullbackState = pullbackState;
        RunnerState = runnerState;
        SupervisorState = supervisorState;
        Confidence = confidence;
        PositionOpen = positionOpen;
        EntryWindowOpen = entryWindowOpen;
        AuthoritativeRiskBlock = authoritativeRiskBlock;
        ForceFlatWindow = forceFlatWindow;
    }

    public NarrativeBias NarrativeBias { get; }
    public int NarrativeStrength { get; }
    public ORBState OrbState { get; }
    public PullbackState PullbackState { get; }
    public RunnerState RunnerState { get; }
    public TradeSupervisorState SupervisorState { get; }
    public int Confidence { get; }
    public bool PositionOpen { get; }
    public bool EntryWindowOpen { get; }
    public bool AuthoritativeRiskBlock { get; }
    public bool ForceFlatWindow { get; }
}

public sealed class IntegratedTradingBrainV2Decision
{
    public IntegratedTradingBrainV2Decision(BrainAction action, int confidence, bool entryPermitted,
        bool exitImmediately, DecisionExplanation explanation, IReadOnlyList<string> reasons)
    {
        Action = action;
        Confidence = confidence;
        EntryPermitted = entryPermitted;
        ExitImmediately = exitImmediately;
        Explanation = explanation ?? throw new ArgumentNullException(nameof(explanation));
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    public BrainAction Action { get; }
    public int Confidence { get; }
    public bool EntryPermitted { get; }
    public bool ExitImmediately { get; }
    public DecisionExplanation Explanation { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class IntegratedTradingBrainV2Engine
{
    private readonly DecisionExplainabilityEngine _explainability = new DecisionExplainabilityEngine();

    public IntegratedTradingBrainV2Decision Evaluate(IntegratedTradingBrainV2Input input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var reasons = new List<string>();

        if (input.ForceFlatWindow || input.SupervisorState == TradeSupervisorState.ForceExit)
            return Build(input, BrainAction.ForceExit, 100, false, true, reasons, "Session", "The end-of-day force-flat rule is active.");

        if (input.AuthoritativeRiskBlock)
            return Build(input, BrainAction.Exit, 100, false, input.PositionOpen, reasons, "Risk", "Authoritative risk control blocks trading.");

        if (input.PositionOpen)
        {
            return input.SupervisorState switch
            {
                TradeSupervisorState.Exit => Build(input, BrainAction.Exit, input.Confidence, false, true, reasons, "Supervisor", "The trade thesis is invalidated."),
                TradeSupervisorState.Reduce => Build(input, BrainAction.Reduce, input.Confidence, false, false, reasons, "Supervisor", "Exposure should be reduced."),
                TradeSupervisorState.Protect or TradeSupervisorState.TightenStop => Build(input, BrainAction.Protect, input.Confidence, false, false, reasons, "Supervisor", "Open profit requires protection."),
                TradeSupervisorState.PromoteRunner => Build(input, BrainAction.PromoteRunner, input.Confidence, false, false, reasons, "Runner", "Trend persistence supports runner promotion."),
                _ => Build(input, BrainAction.Hold, input.Confidence, false, false, reasons, "Supervisor", "The open-position thesis remains valid.")
            };
        }

        var directionalOrb = input.OrbState == ORBState.BreakoutConfirmed || input.OrbState == ORBState.RetestQualified;
        var qualifiedPullback = input.PullbackState == PullbackState.Healthy || input.PullbackState == PullbackState.DeepHealthy || input.PullbackState == PullbackState.Retest;
        var entryQualified = input.EntryWindowOpen && directionalOrb && qualifiedPullback && input.NarrativeStrength >= 65 && input.Confidence >= 70;

        if (entryQualified && input.NarrativeBias == NarrativeBias.Bullish)
            return Build(input, BrainAction.EnterLong, input.Confidence, true, false, reasons, "Entry", "Bullish narrative, ORB, and pullback evidence align.");
        if (entryQualified && input.NarrativeBias == NarrativeBias.Bearish)
            return Build(input, BrainAction.EnterShort, input.Confidence, true, false, reasons, "Entry", "Bearish narrative, ORB, and pullback evidence align.");

        return Build(input, BrainAction.StandAside, input.Confidence, false, false, reasons, "Entry", "Evidence is incomplete or the entry window is closed.");
    }

    private IntegratedTradingBrainV2Decision Build(IntegratedTradingBrainV2Input input, BrainAction action,
        int confidence, bool entryPermitted, bool exitImmediately, List<string> reasons, string source, string message)
    {
        reasons.Add(message);
        var explanationDecision = action switch
        {
            BrainAction.EnterLong => ExplanationDecision.Long,
            BrainAction.EnterShort => ExplanationDecision.Short,
            BrainAction.Hold => ExplanationDecision.Hold,
            BrainAction.Protect => ExplanationDecision.Protect,
            BrainAction.Reduce => ExplanationDecision.Reduce,
            BrainAction.PromoteRunner => ExplanationDecision.PromoteRunner,
            BrainAction.Exit => ExplanationDecision.Exit,
            BrainAction.ForceExit => ExplanationDecision.ForceExit,
            _ => ExplanationDecision.None
        };
        var category = action == BrainAction.StandAside ? EvidenceCategory.Blocking : EvidenceCategory.Supporting;
        var explanation = _explainability.Explain(new ExplainabilityInput(explanationDecision, confidence,
            new[] { new DecisionEvidence(category, source, message) },
            action == BrainAction.PromoteRunner || input.RunnerState == RunnerState.ConfirmedRunner || input.RunnerState == RunnerState.EliteRunner,
            action == BrainAction.Exit,
            input.AuthoritativeRiskBlock,
            action == BrainAction.ForceExit));
        return new IntegratedTradingBrainV2Decision(action, confidence, entryPermitted, exitImmediately, explanation, reasons);
    }
}
