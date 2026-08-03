using System;

namespace ISE.TradePlanning;

/// <summary>Creates deterministic platform-independent trade plans.</summary>
public sealed class TradePlanningEngine
{
    /// <summary>Evaluates approved upstream decisions and creates a trade plan.</summary>
    public TradePlan Evaluate(TradePlanInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (!input.StrategyApproved) return Rejected(TradePlanReason.StrategyNotApproved);
        if (!input.RiskApproved) return Rejected(TradePlanReason.RiskNotApproved);
        if (!input.ObjectivePermitted) return Rejected(TradePlanReason.ObjectiveNotPermitted);
        if (input.Direction == TradeDirection.None) return Rejected(TradePlanReason.InvalidDirection);
        if (input.Contracts <= 0) return Rejected(TradePlanReason.InvalidContracts);
        if (input.StopDistance <= 0) return Rejected(TradePlanReason.InvalidStopDistance);
        if (input.RewardMultiple <= 0) return Rejected(TradePlanReason.InvalidRewardMultiple);

        var entry = input.ReferencePrice;
        var stop = input.Direction == TradeDirection.Long
            ? entry - input.StopDistance
            : entry + input.StopDistance;
        var targetDistance = input.StopDistance * input.RewardMultiple;
        var target = input.Direction == TradeDirection.Long
            ? entry + targetDistance
            : entry - targetDistance;

        return new TradePlan(true, TradePlanReason.Planned, input.Direction, input.EntryOrderType,
            input.Contracts, entry, stop, target, input.RewardMultiple);
    }

    private static TradePlan Rejected(TradePlanReason reason) =>
        new TradePlan(false, reason, TradeDirection.None, EntryOrderType.Market, 0, 0m, 0m, 0m, 0m);
}
