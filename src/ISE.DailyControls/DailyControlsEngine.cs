using System;

namespace ISE.DailyControls;

/// <summary>Evaluates account-level limits before another trade may be initiated.</summary>
public sealed class DailyControlsEngine
{
    /// <summary>Evaluates the supplied account state.</summary>
    public DailyControlDecision Evaluate(DailyControlInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var profile = input.Profile;

        if (input.SessionShutdown)
            return new DailyControlDecision(DailyControlAction.ForceFlat, DailyControlReason.SessionShutdown, 0m);

        if (input.AccountPaused)
            return new DailyControlDecision(DailyControlAction.StopTrading, DailyControlReason.AccountPaused, 0m);

        if (input.RealizedProfitLoss >= profile.MaximumDailyProfit)
            return new DailyControlDecision(DailyControlAction.StopTrading, DailyControlReason.MaximumDailyProfitReached, 0m);

        if (input.RealizedProfitLoss <= -profile.DailyLossLimit)
            return new DailyControlDecision(DailyControlAction.StopTrading, DailyControlReason.DailyLossLimitReached, 0m);

        if (input.ConsecutiveLosses >= profile.MaximumConsecutiveLosses)
            return new DailyControlDecision(DailyControlAction.StopTrading, DailyControlReason.ConsecutiveLossLimitReached, 0m);

        if (input.TradesToday >= profile.MaximumTradesPerDay)
            return new DailyControlDecision(DailyControlAction.StopTrading, DailyControlReason.MaximumTradesReached, 0m);

        if (input.RealizedProfitLoss >= profile.PreferredDailyProfit)
        {
            if (profile.AllowExceptionalSetupsAfterTarget && input.ExceptionalSetup)
                return new DailyControlDecision(DailyControlAction.ReduceRisk, DailyControlReason.ExceptionalSetupPermitted, profile.ReducedRiskMultiplier);

            return new DailyControlDecision(DailyControlAction.StopTrading, DailyControlReason.PreferredTargetReached, 0m);
        }

        return new DailyControlDecision(DailyControlAction.AllowTrading, DailyControlReason.TradingPermitted, 1m);
    }
}
