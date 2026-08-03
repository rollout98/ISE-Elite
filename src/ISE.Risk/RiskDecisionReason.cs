namespace ISE.Risk;

/// <summary>Explains why a risk request was approved or rejected.</summary>
public enum RiskDecisionReason
{
    /// <summary>The trade candidate passed all configured risk checks.</summary>
    Approved,

    /// <summary>The upstream signal was not eligible for execution.</summary>
    SignalNotEligible,

    /// <summary>The account has reached its configured daily loss limit.</summary>
    DailyLossLimitReached,

    /// <summary>The account has reached its configured daily trade limit.</summary>
    TradeLimitReached,

    /// <summary>The account has no usable drawdown capacity remaining.</summary>
    InsufficientDrawdownRoom,

    /// <summary>One contract would exceed the available risk capacity.</summary>
    StopRiskExceedsCapacity,

    /// <summary>No contract quantity is permitted after applying all limits.</summary>
    NoContractsAllowed
}
