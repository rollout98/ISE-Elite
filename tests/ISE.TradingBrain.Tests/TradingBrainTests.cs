using System;
using ISE.DailyControls;
using ISE.DecisionOrchestration;
using ISE.OpportunityScoring;
using ISE.Risk;
using ISE.TradePlanning;
using Xunit;

namespace ISE.TradingBrain.Tests;

public sealed class TradingBrainTests
{
    [Fact]
    public void Elite_candidate_creates_three_execution_orders()
    {
        var result = Evaluate(OpportunityGrade.Elite, DailyControlAction.AllowTrading, true, true);
        Assert.Equal(DecisionAction.ApproveFullSize, result.Decision.Action);
        Assert.True(result.ExecutionPrepared);
        Assert.Equal(3, result.ExecutionCommands!.Orders.Count);
    }

    [Fact]
    public void Risk_rejection_creates_no_execution_commands()
    {
        var result = Evaluate(OpportunityGrade.Elite, DailyControlAction.AllowTrading, false, true);
        Assert.Equal(DecisionAction.Reject, result.Decision.Action);
        Assert.False(result.ExecutionPrepared);
        Assert.Null(result.ExecutionCommands);
    }

    [Fact]
    public void Force_flat_creates_no_new_entry_orders()
    {
        var result = Evaluate(OpportunityGrade.Elite, DailyControlAction.ForceFlat, true, true);
        Assert.Equal(DecisionAction.ForceFlat, result.Decision.Action);
        Assert.False(result.ExecutionPrepared);
    }

    [Fact]
    public void Duplicate_trade_plan_is_rejected_by_execution_boundary()
    {
        var brain = new TradingBrain();
        var input = CreateInput(OpportunityGrade.Elite, DailyControlAction.AllowTrading, true, true);
        Assert.True(brain.Evaluate(input).ExecutionPrepared);
        Assert.False(brain.Evaluate(input).ExecutionPrepared);
    }

    private static TradingBrainDecision Evaluate(OpportunityGrade grade, DailyControlAction dailyAction, bool riskApproved, bool planApproved) =>
        new TradingBrain().Evaluate(CreateInput(grade, dailyAction, riskApproved, planApproved));

    private static TradingBrainInput CreateInput(OpportunityGrade grade, DailyControlAction dailyAction, bool riskApproved, bool planApproved)
    {
        var opportunity = new OpportunityScoreSnapshot(95m, grade, grade == OpportunityGrade.Reject ? 0m : 1m, grade != OpportunityGrade.Reject, "test");
        var daily = new DailyControlDecision(dailyAction, (DailyControlReason)0, dailyAction == DailyControlAction.AllowTrading ? 1m : 0m);
        var risk = new RiskDecision(riskApproved, riskApproved ? 4 : 0, riskApproved ? 400m : 0m, (RiskDecisionReason)0);
        var plan = new TradePlan(planApproved, (TradePlanReason)0, (TradeDirection)0, (EntryOrderType)0, planApproved ? 4 : 0, 100m, 99m, 102m, 2m);
        var decisionInput = new DecisionOrchestrationInput(true, true, opportunity, daily, risk, plan);
        return new TradingBrainInput(Guid.NewGuid(), decisionInput);
    }
}
