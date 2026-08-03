using System;
using System.Collections.Generic;

namespace ISE.Execution;

/// <summary>Contains the execution orders created for one trade plan.</summary>
public sealed class ExecutionCommandSet
{
    /// <summary>Initializes an execution command-set result.</summary>
    public ExecutionCommandSet(bool accepted, ExecutionResultReason reason, Guid tradePlanId, IReadOnlyList<ExecutionOrder> orders)
    {
        if (orders is null) throw new ArgumentNullException(nameof(orders));

        Accepted = accepted;
        Reason = reason;
        TradePlanId = tradePlanId;
        Orders = orders;
    }

    /// <summary>Gets whether command creation was accepted.</summary>
    public bool Accepted { get; }
    /// <summary>Gets the outcome reason.</summary>
    public ExecutionResultReason Reason { get; }
    /// <summary>Gets the originating trade-plan identifier.</summary>
    public Guid TradePlanId { get; }
    /// <summary>Gets the generated execution orders.</summary>
    public IReadOnlyList<ExecutionOrder> Orders { get; }
}
