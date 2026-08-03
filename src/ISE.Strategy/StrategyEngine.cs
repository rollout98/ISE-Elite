using System;

namespace ISE.Strategy;

/// <summary>Qualifies normalized market evidence against a selected strategy playbook.</summary>
public sealed class StrategyEngine
{
    /// <summary>Evaluates a strategy candidate.</summary>
    public StrategyDecision Evaluate(StrategyInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var profile = input.Profile;
        if (!input.SessionEligible)
            return Reject(profile, StrategyDecisionReason.SessionNotEligible);
        if (!input.SignalEligible)
            return Reject(profile, StrategyDecisionReason.SignalNotEligible);
        if (input.Confidence < profile.MinimumConfidence)
            return Reject(profile, StrategyDecisionReason.ConfidenceBelowMinimum);
        if (profile.RequiresLiquidityEvent && !input.LiquidityEventPresent)
            return Reject(profile, StrategyDecisionReason.LiquidityRequirementNotMet);
        if (profile.RequiresStructureAlignment && !input.StructureAligned)
            return Reject(profile, StrategyDecisionReason.StructureNotAligned);
        if (profile.RequiresOrderFlowAlignment && !input.OrderFlowAligned)
            return Reject(profile, StrategyDecisionReason.OrderFlowNotAligned);

        if (profile.StrategyId == StrategyId.NewYorkOpenReversal && !input.PriorTrendOpposedSignal)
            return Reject(profile, StrategyDecisionReason.ReversalContextMissing);
        if (profile.StrategyId == StrategyId.NewYorkContinuation && !input.TrendAligned)
            return Reject(profile, StrategyDecisionReason.ContinuationContextMissing);

        return new StrategyDecision(true, profile.StrategyId, StrategyDecisionReason.Qualified);
    }

    private static StrategyDecision Reject(StrategyProfile profile, StrategyDecisionReason reason) =>
        new StrategyDecision(false, profile.StrategyId, reason);
}
