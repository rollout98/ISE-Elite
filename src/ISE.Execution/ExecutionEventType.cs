namespace ISE.Execution;

/// <summary>Identifies a platform-independent execution event.</summary>
public enum ExecutionEventType
{
    /// <summary>An order command was created.</summary>
    CommandCreated,
    /// <summary>An order was accepted by the platform.</summary>
    OrderAccepted,
    /// <summary>An order received a partial fill.</summary>
    OrderPartiallyFilled,
    /// <summary>An order was completely filled.</summary>
    OrderFilled,
    /// <summary>An order was cancelled.</summary>
    OrderCancelled,
    /// <summary>An order was rejected.</summary>
    OrderRejected,
    /// <summary>An order failed.</summary>
    OrderFailed
}
