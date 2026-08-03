using ISE.DailyControls;
using ISE.DecisionOrchestration;
using ISE.OpportunityScoring;
using ISE.Risk;
using ISE.TradePlanning;
using Xunit;

namespace ISE.EndToEnd.Tests;

public sealed class TradingBrainScenarioTests
{
    [Fact]
    public void Elite_new_york_reversal_is_authorized_at_full_size()
    {
        var result = Evaluate(
            strategyQualified: true,
            confirmationComplete: true,
            grade: OpportunityGrade.Elite,
            opportunityMultiplier: 1m,
            dailyAction: DailyControlAction.AllowTrading,
            dailyMultiplier: 1m,
            riskApproved: true,
            riskContracts: 4,
            planApproved: true,
            planContracts: 4);

        Assert.Equal(DecisionAction.ApproveFullSize, result.Action);
        Assert.Equal(4, result.AuthorizedContracts);
        Assert.True(result.ExecutionAuthorized);
    }

    [Fact]
    public void Funded_account_after_preferred_target_continues_at_reduced_size()
    {
        var result = Evaluate(
            strategyQualified: true,
            confirmationComplete: true,
            grade: OpportunityGrade.A,
            opportunityMultiplier: 1m,
            dailyAction: DailyControlAction.ReduceRisk,
            dailyMultiplier: 0.5m,
            riskApproved: true,
            riskContracts: 4,
            planApproved: true,
            planContracts: 4);

        Assert.Equal(DecisionAction.ApproveReducedSize, result.Action);
        Assert.Equal(2, result.AuthorizedContracts);
    }

    [Fact]
    public void Evaluation_account_uses_more_conservative_approved_contract_limit()
    {
        var result = Evaluate(
            strategyQualified: true,
            confirmationComplete: true,
            grade: OpportunityGrade.Elite,
            opportunityMultiplier: 1m,
            dailyAction: DailyControlAction.AllowTrading,
            dailyMultiplier: 1m,
            riskApproved: true,
            riskContracts: 2,
            planApproved: true,
            planContracts: 5);

        Assert.Equal(DecisionAction.ApproveFullSize, result.Action);
        Assert.Equal(2, result.AuthorizedContracts);
    }

    [Fact]
    public void News_lockout_rejects_an_otherwise_elite_candidate()
    {
        var result = Evaluate(
            strategyQualified: true,
            confirmationComplete: true,
            grade: OpportunityGrade.Reject,
            opportunityMultiplier: 0m,
            dailyAction: DailyControlAction.AllowTrading,
            dailyMultiplier: 1m,
            riskApproved: true,
            riskContracts: 4,
            planApproved: true,
            planContracts: 4);

        Assert.Equal(DecisionAction.Reject, result.Action);
        Assert.Equal(DecisionReason.OpportunityRejected, result.Reason);
        Assert.False(result.ExecutionAuthorized);
    }

    [Fact]
    public void Daily_loss_limit_stops_new_trades()
    {
        var result = Evaluate(
            strategyQualified: true,
            confirmationComplete: true,
            grade: OpportunityGrade.Elite,
            opportunityMultiplier: 1m,
            dailyAction: DailyControlAction.StopTrading,
            dailyMultiplier: 0m,
            riskApproved: true,
            riskContracts: 4,
            planApproved: true,
            planContracts: 4);

        Assert.Equal(DecisionAction.StopTrading, result.Action);
        Assert.Equal(0, result.AuthorizedContracts);
    }

    [Fact]
    public void Session_shutdown_forces_account_flat()
    {
        var result = Evaluate(
            strategyQualified: true,
            confirmationComplete: true,
            grade: OpportunityGrade.Elite,
            opportunityMultiplier: 1m,
            dailyAction: DailyControlAction.ForceFlat,
            dailyMultiplier: 0m,
            riskApproved: true,
            riskContracts: 4,
            planApproved: true,
            planContracts: 4);

        Assert.Equal(DecisionAction.ForceFlat, result.Action);
        Assert.False(result.ExecutionAuthorized);
    }

    private static DecisionOrchestrationSnapshot Evaluate(
        bool strategyQualified,
        bool confirmationComplete,
        OpportunityGrade grade,
        decimal opportunityMultiplier,
        DailyControlAction dailyAction,
        decimal dailyMultiplier,
        bool riskApproved,
        int riskContracts,
        bool planApproved,
        int planContracts)
    {
        var opportunity = new OpportunityScoreSnapshot(
            grade == OpportunityGrade.Elite ? 95m : grade == OpportunityGrade.A ? 85m : 0m,
            grade,
            opportunityMultiplier,
            grade != OpportunityGrade.Reject,
            "scenario");

        var daily = new DailyControlDecision(dailyAction, (DailyControlReason)0, dailyMultiplier);
        var risk = new RiskDecision(
            riskApproved,
            riskApproved ? riskContracts : 0,
            riskApproved ? riskContracts * 100m : 0m,
            (RiskDecisionReason)0);
        var plan = new TradePlan(
            planApproved,
            (TradePlanReason)0,
            (TradeDirection)0,
            (EntryOrderType)0,
            planApproved ? planContracts : 0,
            100m,
            99m,
            102m,
            2m);

        return new DecisionOrchestrationEngine().Evaluate(
            new DecisionOrchestrationInput(
                strategyQualified,
                confirmationComplete,
                opportunity,
                daily,
                risk,
                plan));
    }
}
