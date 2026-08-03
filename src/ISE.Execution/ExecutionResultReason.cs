namespace ISE.Execution;

/// <summary>Explains the outcome of an execution operation.</summary>
public enum ExecutionResultReason
{
    /// <summary>The operation completed successfully.</summary>
    Accepted,
    /// <summary>The trade plan was not approved.</summary>
    TradePlanNotApproved,
    /// <summary>The trade plan identifier was invalid.</summary>
    InvalidTradePlanId,
    /// <summary>The trade plan was already submitted.</summary>
    DuplicateTradePlan,
    /// <summary>The order transition was not valid for its current state.</summary>
    InvalidStateTransition,
    /// <summary>The fill quantity was invalid.</summary>
    InvalidFillQuantity,
    /// <summary>The platform rejected the order.</summary>
    PlatformRejected,
    /// <summary>The execution operation failed.</summary>
    PlatformFailure
}
