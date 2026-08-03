using System;
using System.Collections.Generic;

namespace ISE.ExecutionIntelligence;

/// <summary>Represents a selected execution approach.</summary>
public sealed class ExecutionRecommendation
{
    /// <summary>Initializes an execution recommendation.</summary>
    public ExecutionRecommendation(bool approved, ExecutionMode mode, int contracts, ExecutionReason reason, int limitOffsetTicks, int maximumSlippageTicks, bool allowChasing, IReadOnlyList<string> notes)
    {
        if (contracts < 0) throw new ArgumentOutOfRangeException(nameof(contracts));
        if (limitOffsetTicks < 0) throw new ArgumentOutOfRangeException(nameof(limitOffsetTicks));
        if (maximumSlippageTicks < 0) throw new ArgumentOutOfRangeException(nameof(maximumSlippageTicks));
        Approved = approved;
        Mode = mode;
        Contracts = contracts;
        Reason = reason;
        LimitOffsetTicks = limitOffsetTicks;
        MaximumSlippageTicks = maximumSlippageTicks;
        AllowChasing = allowChasing;
        Notes = notes ?? throw new ArgumentNullException(nameof(notes));
    }

    /// <summary>Gets whether an order may be submitted.</summary>
    public bool Approved { get; }
    /// <summary>Gets the selected execution mode.</summary>
    public ExecutionMode Mode { get; }
    /// <summary>Gets the adjusted contract quantity.</summary>
    public int Contracts { get; }
    /// <summary>Gets the primary reason.</summary>
    public ExecutionReason Reason { get; }
    /// <summary>Gets the limit offset in ticks.</summary>
    public int LimitOffsetTicks { get; }
    /// <summary>Gets the maximum permitted slippage in ticks.</summary>
    public int MaximumSlippageTicks { get; }
    /// <summary>Gets whether controlled chasing is permitted.</summary>
    public bool AllowChasing { get; }
    /// <summary>Gets explainable notes.</summary>
    public IReadOnlyList<string> Notes { get; }
}
