using ISE.AccountObjectives;
using Xunit;

namespace ISE.AccountObjectives.Tests;

public sealed class AccountObjectiveEngineTests
{
    [Fact]
    public void One_day_evaluation_uses_remaining_target_as_daily_objective()
    {
        var profile = new AccountObjectiveProfile("OneDay Firm", AccountStage.Evaluation, ObjectiveMode.PassEvaluation, 3000m, 1, 1, 500m, 3000m, false);
        var decision = new AccountObjectiveEngine().Evaluate(new AccountObjectiveInput(profile, 0m, 0m, 0, true, true, false));

        Assert.True(decision.TradingPermitted);
        Assert.Equal(3000m, decision.DailyObjective);
    }

    [Fact]
    public void Five_day_evaluation_spreads_remaining_target_across_days()
    {
        var profile = new AccountObjectiveProfile("FiveDay Firm", AccountStage.Evaluation, ObjectiveMode.PassEvaluation, 3000m, 5, 5, 500m, 1500m, false);
        var decision = new AccountObjectiveEngine().Evaluate(new AccountObjectiveInput(profile, 1200m, 0m, 1, true, true, false));

        Assert.Equal(500m, decision.DailyObjective);
        Assert.Equal(1800m, decision.AccountRemaining);
    }

    [Fact]
    public void Funded_account_stops_at_preferred_target_without_exceptional_setup()
    {
        var profile = new AccountObjectiveProfile("Income Firm", AccountStage.Funded, ObjectiveMode.Income, 0m, 1, 1, 750m, 10000m, true);
        var decision = new AccountObjectiveEngine().Evaluate(new AccountObjectiveInput(profile, 0m, 750m, 10, true, true, false));

        Assert.False(decision.TradingPermitted);
        Assert.Equal(ObjectiveDecisionReason.ExceptionalSetupRequired, decision.Reason);
    }

    [Fact]
    public void Exceptional_funded_setup_can_continue_below_firm_daily_maximum()
    {
        var profile = new AccountObjectiveProfile("Income Firm", AccountStage.Funded, ObjectiveMode.Income, 0m, 1, 1, 750m, 10000m, true);
        var decision = new AccountObjectiveEngine().Evaluate(new AccountObjectiveInput(profile, 0m, 1500m, 10, true, true, true));

        Assert.True(decision.TradingPermitted);
        Assert.Equal(ObjectiveDecisionReason.TradingPermitted, decision.Reason);
    }
}
