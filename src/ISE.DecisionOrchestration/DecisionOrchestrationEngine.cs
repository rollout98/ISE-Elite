using System;
using ISE.DailyControls;
using ISE.OpportunityScoring;

namespace ISE.DecisionOrchestration;

/// <summary>Combines authoritative engine outputs into one final execution decision.</summary>
public sealed class DecisionOrchestrationEngine
{
    /// <summary>Evaluates the complete decision pipeline.</summary>
    public DecisionOrchestrationSnapshot Evaluate(DecisionOrchestrationInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        if (input.DailyControls.Action == DailyControlAction.ForceFlat)
            return Reject(DecisionAction.ForceFlat, DecisionReason.DailyControlsRequireFlat);
        if (input.DailyControls.Action == DailyControlAction.StopTrading)
            return Reject(DecisionAction.StopTrading, DecisionReason.DailyControlsStoppedTrading);
        if (!input.StrategyQualified)
            return Reject(DecisionAction.Reject, DecisionReason.StrategyRejected);
        if (!input.ConfirmationComplete)
            return Reject(DecisionAction.Wait, DecisionReason.MoreConfirmationRequired);
        if (!input.Opportunity.Eligible || input.Opportunity.Grade == OpportunityGrade.Reject)
            return Reject(DecisionAction.Reject, DecisionReason.OpportunityRejected);
        if (!input.Risk.Approved || input.Risk.Contracts <= 0)
            return Reject(DecisionAction.Reject, DecisionReason.RiskRejected);
        if (!input.TradePlan.Approved || input.TradePlan.Contracts <= 0)
            return Reject(DecisionAction.Reject, DecisionReason.TradePlanRejected);

        var multiplier = Math.Min(input.Opportunity.SizeMultiplier, input.DailyControls.RiskMultiplier);
        var approvedBase = Math.Min(input.Risk.Contracts, input.TradePlan.Contracts);
        var contracts = Math.Max(1, (int)Math.Floor(approvedBase * multiplier));

        if (multiplier < 1m || input.Opportunity.Grade == OpportunityGrade.B || input.DailyControls.Action == DailyControlAction.ReduceRisk)
            return new DecisionOrchestrationSnapshot(DecisionAction.ApproveReducedSize, DecisionReason.ReducedRiskRequired, contracts, multiplier, input.TradePlan);
        if (input.Opportunity.Grade == OpportunityGrade.Elite)
            return new DecisionOrchestrationSnapshot(DecisionAction.ApproveFullSize, DecisionReason.EliteOpportunityApproved, contracts, multiplier, input.TradePlan);

        return new DecisionOrchestrationSnapshot(DecisionAction.ApproveNormalSize, DecisionReason.OpportunityApproved, contracts, multiplier, input.TradePlan);
    }

    private static DecisionOrchestrationSnapshot Reject(DecisionAction action, DecisionReason reason) =>
        new DecisionOrchestrationSnapshot(action, reason, 0, 0m, null);
}
