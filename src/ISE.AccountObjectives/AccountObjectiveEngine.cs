using System;

namespace ISE.AccountObjectives;

/// <summary>Aligns trading permission with account goals and firm constraints.</summary>
public sealed class AccountObjectiveEngine
{
    /// <summary>Evaluates whether trading may continue and calculates today's objective.</summary>
    public AccountObjectiveDecision Evaluate(AccountObjectiveInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        var profile = input.Profile;
        var accountRemaining = profile.Stage == AccountStage.Evaluation
            ? Math.Max(0m, profile.AccountProfitTarget - input.CumulativeProfit)
            : 0m;

        var dailyObjective = CalculateDailyObjective(input, accountRemaining);
        var dailyRemaining = Math.Max(0m, dailyObjective - input.TodayProfit);

        if (!input.StrategyQualified)
            return Decide(false, dailyObjective, dailyRemaining, accountRemaining, ObjectiveDecisionReason.StrategyNotQualified);
        if (!input.RiskApproved)
            return Decide(false, dailyObjective, dailyRemaining, accountRemaining, ObjectiveDecisionReason.RiskNotApproved);
        if (input.TodayProfit >= profile.MaximumDailyProfit)
            return Decide(false, dailyObjective, 0m, accountRemaining, ObjectiveDecisionReason.MaximumDailyProfitReached);
        if (profile.Stage == AccountStage.Evaluation && accountRemaining == 0m)
            return Decide(false, dailyObjective, 0m, 0m, ObjectiveDecisionReason.EvaluationTargetReached);
        if (input.TodayProfit >= dailyObjective)
        {
            if (profile.AllowExceptionalExtension && input.ExceptionalSetup)
                return Decide(true, dailyObjective, 0m, accountRemaining, ObjectiveDecisionReason.TradingPermitted);
            if (profile.AllowExceptionalExtension)
                return Decide(false, dailyObjective, 0m, accountRemaining, ObjectiveDecisionReason.ExceptionalSetupRequired);
            return Decide(false, dailyObjective, 0m, accountRemaining, ObjectiveDecisionReason.DailyObjectiveReached);
        }

        return Decide(true, dailyObjective, dailyRemaining, accountRemaining, ObjectiveDecisionReason.TradingPermitted);
    }

    private static decimal CalculateDailyObjective(AccountObjectiveInput input, decimal accountRemaining)
    {
        var profile = input.Profile;
        if (profile.Stage == AccountStage.Funded || profile.Mode != ObjectiveMode.PassEvaluation)
            return profile.PreferredDailyProfit;

        var daysRemaining = Math.Max(1, profile.PlannedPassDays - input.CompletedTradingDays);
        var requiredAverage = accountRemaining / daysRemaining;
        return Math.Min(profile.MaximumDailyProfit, Math.Max(profile.PreferredDailyProfit, requiredAverage));
    }

    private static AccountObjectiveDecision Decide(bool permitted, decimal objective, decimal dailyRemaining, decimal accountRemaining, ObjectiveDecisionReason reason)
        => new AccountObjectiveDecision(permitted, objective, dailyRemaining, accountRemaining, reason);
}
