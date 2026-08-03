namespace ISE.TradePlanning;

/// <summary>Represents a platform-independent trade plan.</summary>
public sealed class TradePlan
{
    /// <summary>Initializes a trade plan result.</summary>
    public TradePlan(bool approved, TradePlanReason reason, TradeDirection direction, EntryOrderType entryOrderType, int contracts, decimal entryPrice, decimal stopPrice, decimal targetPrice, decimal rewardMultiple)
    {
        Approved = approved;
        Reason = reason;
        Direction = direction;
        EntryOrderType = entryOrderType;
        Contracts = contracts;
        EntryPrice = entryPrice;
        StopPrice = stopPrice;
        TargetPrice = targetPrice;
        RewardMultiple = rewardMultiple;
    }

    /// <summary>Gets whether the plan is approved.</summary>
    public bool Approved { get; }
    /// <summary>Gets the outcome reason.</summary>
    public TradePlanReason Reason { get; }
    /// <summary>Gets the planned direction.</summary>
    public TradeDirection Direction { get; }
    /// <summary>Gets the planned order type.</summary>
    public EntryOrderType EntryOrderType { get; }
    /// <summary>Gets the planned contract quantity.</summary>
    public int Contracts { get; }
    /// <summary>Gets the planned entry price.</summary>
    public decimal EntryPrice { get; }
    /// <summary>Gets the planned protective stop price.</summary>
    public decimal StopPrice { get; }
    /// <summary>Gets the planned profit target price.</summary>
    public decimal TargetPrice { get; }
    /// <summary>Gets the planned reward-to-risk multiple.</summary>
    public decimal RewardMultiple { get; }
}
