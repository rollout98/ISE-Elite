using System;

namespace ISE.TradePlanning;

/// <summary>Contains approved upstream decisions and price-planning parameters.</summary>
public sealed class TradePlanInput
{
    /// <summary>Initializes a trade-planning request.</summary>
    public TradePlanInput(bool strategyApproved, bool riskApproved, bool objectivePermitted, TradeDirection direction, int contracts, decimal referencePrice, decimal stopDistance, decimal rewardMultiple, EntryOrderType entryOrderType)
    {
        if (contracts < 0) throw new ArgumentOutOfRangeException(nameof(contracts));
        if (referencePrice <= 0) throw new ArgumentOutOfRangeException(nameof(referencePrice));
        StrategyApproved = strategyApproved;
        RiskApproved = riskApproved;
        ObjectivePermitted = objectivePermitted;
        Direction = direction;
        Contracts = contracts;
        ReferencePrice = referencePrice;
        StopDistance = stopDistance;
        RewardMultiple = rewardMultiple;
        EntryOrderType = entryOrderType;
    }

    /// <summary>Gets whether the strategy candidate is approved.</summary>
    public bool StrategyApproved { get; }
    /// <summary>Gets whether risk approved the trade.</summary>
    public bool RiskApproved { get; }
    /// <summary>Gets whether the account objective permits another trade.</summary>
    public bool ObjectivePermitted { get; }
    /// <summary>Gets the trade direction.</summary>
    public TradeDirection Direction { get; }
    /// <summary>Gets the approved contract quantity.</summary>
    public int Contracts { get; }
    /// <summary>Gets the reference entry price.</summary>
    public decimal ReferencePrice { get; }
    /// <summary>Gets the absolute stop distance in price units.</summary>
    public decimal StopDistance { get; }
    /// <summary>Gets the requested reward-to-risk multiple.</summary>
    public decimal RewardMultiple { get; }
    /// <summary>Gets the preferred entry order type.</summary>
    public EntryOrderType EntryOrderType { get; }
}
