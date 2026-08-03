using ISE.DailyControls;
using ISE.DecisionOrchestration;
using ISE.OpportunityScoring;
using ISE.Risk;
using ISE.TradePlanning;
using Xunit;

namespace ISE.DecisionOrchestration.Tests;

public sealed class DecisionOrchestrationEngineTests
{
    [Fact]
    public void Elite_candidate_authorizes_full_size()
    {
        var result = Evaluate(true, true, OpportunityGrade.Elite, 1m, DailyControlAction.AllowTrading, 1m, true, true);
        Assert.Equal(DecisionAction.ApproveFullSize, result.Action);
        Assert.Equal(4, result.AuthorizedContracts);
    }

    [Fact]
    public void Reduced_risk_uses_most_restrictive_multiplier()
    {
        var result = Evaluate(true, true, OpportunityGrade.A, 1m, DailyControlAction.ReduceRisk, 0.5m, true, true);
        Assert.Equal(DecisionAction.ApproveReducedSize, result.Action);
        Assert.Equal(2, result.AuthorizedContracts);
    }

    [Fact]
    public void Risk_rejection_blocks_execution()
    {
        var result = Evaluate(true, true, OpportunityGrade.Elite, 1m, DailyControlAction.AllowTrading, 1m, false, true);
        Assert.Equal(DecisionReason.RiskRejected, result.Reason);
        Assert.False(result.ExecutionAuthorized);
    }

    [Fact]
    public void Force_flat_overrides_all_other_approvals()
    {
        var result = Evaluate(true, true, OpportunityGrade.Elite, 1m, DailyControlAction.ForceFlat, 0m, true, true);
        Assert.Equal(DecisionAction.ForceFlat, result.Action);
        Assert.Equal(0, result.AuthorizedContracts);
    }

    private static DecisionOrchestrationSnapshot Evaluate(bool strategyQualified, bool confirmationComplete, OpportunityGrade grade, decimal opportunityMultiplier, DailyControlAction dailyAction, decimal dailyMultiplier, bool riskApproved, bool planApproved)
    {
        var opportunity = new OpportunityScoreSnapshot(95m, grade, opportunityMultiplier, grade != OpportunityGrade.Reject, "test");
        var daily = new DailyControlDecision(dailyAction, (DailyControlReason)0, dailyMultiplier);
        var risk = new RiskDecision(riskApproved, riskApproved ? 4 : 0, riskApproved ? 400m : 0m, (RiskDecisionReason)0);
        var plan = new TradePlan(planApproved, (TradePlanReason)0, (TradeDirection)0, (EntryOrderType)0, planApproved ? 4 : 0, 100m, 99m, 102m, 2m);
        return new DecisionOrchestrationEngine().Evaluate(new DecisionOrchestrationInput(strategyQualified, confirmationComplete, opportunity, daily, risk, plan));
    }
}
