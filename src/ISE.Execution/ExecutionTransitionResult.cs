using System;

namespace ISE.Execution;

/// <summary>Represents the result of applying an order lifecycle event.</summary>
public sealed class ExecutionTransitionResult
{
    /// <summary>Initializes an execution transition result.</summary>
    public ExecutionTransitionResult(bool accepted, ExecutionResultReason reason, ExecutionOrder order)
    {
        Order = order ?? throw new ArgumentNullException(nameof(order));
        Accepted = accepted;
        Reason = reason;
    }

    /// <summary>Gets whether the transition was accepted.</summary>
    public bool Accepted { get; }
    /// <summary>Gets the transition outcome reason.</summary>
    public ExecutionResultReason Reason { get; }
    /// <summary>Gets the resulting order snapshot.</summary>
    public ExecutionOrder Order { get; }
}
