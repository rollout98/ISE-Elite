namespace ISE.Risk;

/// <summary>Explains why a risk request was approved or rejected.</summary>
public enum RiskDecisionReason
{
    Approved,
    SignalNotEligible,
    DailyLossLimitReached,
    TradeLimitReached,
    InsufficientDrawdownRoom,
    StopRiskExceedsCapacity,
    NoContractsAllowed
}
