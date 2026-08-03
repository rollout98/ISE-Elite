namespace ISE.Execution;

/// <summary>Describes the lifecycle state of an execution order.</summary>
public enum ExecutionOrderState
{
    /// <summary>The order is prepared but has not been submitted.</summary>
    PendingSubmission,
    /// <summary>The contingent order is held until the entry is filled.</summary>
    Held,
    /// <summary>The platform accepted the order and it is working.</summary>
    Working,
    /// <summary>The order has received a partial fill.</summary>
    PartiallyFilled,
    /// <summary>The order has been completely filled.</summary>
    Filled,
    /// <summary>The order was cancelled.</summary>
    Cancelled,
    /// <summary>The platform rejected the order.</summary>
    Rejected,
    /// <summary>The order failed because of a transport or platform error.</summary>
    Failed
}
