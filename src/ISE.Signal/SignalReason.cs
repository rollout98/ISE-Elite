namespace ISE.Signal;

/// <summary>Identifies evidence or gating conditions used in a signal decision.</summary>
public enum SignalReason
{
    /// <summary>The active session permits trading.</summary>
    TradingPermitted,

    /// <summary>The active session blocks trading.</summary>
    TradingBlocked,

    /// <summary>The trend model supports a long decision.</summary>
    BullishTrend,

    /// <summary>The trend model supports a short decision.</summary>
    BearishTrend,

    /// <summary>Confirmed structure supports a long decision.</summary>
    BullishStructure,

    /// <summary>Confirmed structure supports a short decision.</summary>
    BearishStructure,

    /// <summary>Sell-side liquidity was swept and reclaimed.</summary>
    SellSideLiquidityReclaimed,

    /// <summary>Buy-side liquidity was swept and reclaimed.</summary>
    BuySideLiquidityReclaimed,

    /// <summary>Order flow supports a long decision.</summary>
    BullishOrderFlow,

    /// <summary>Order flow supports a short decision.</summary>
    BearishOrderFlow,

    /// <summary>Directional evidence is insufficient or conflicting.</summary>
    ConflictingEvidence,

    /// <summary>The strongest direction did not meet the configured threshold.</summary>
    ConfidenceBelowThreshold
}
