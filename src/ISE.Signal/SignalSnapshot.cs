using System;
using System.Collections.Generic;

namespace ISE.Signal;

/// <summary>Represents an explainable Signal Engine decision.</summary>
public sealed class SignalSnapshot
{
    /// <summary>Creates an immutable signal decision.</summary>
    public SignalSnapshot(
        SignalDirection direction,
        int confidence,
        bool executionEligible,
        IReadOnlyList<SignalReason> reasons)
    {
        if (confidence < 0 || confidence > 100)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 100.");

        Direction = direction;
        Confidence = confidence;
        ExecutionEligible = executionEligible;
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    /// <summary>Gets the directional decision.</summary>
    public SignalDirection Direction { get; }

    /// <summary>Gets the winning directional score from zero to one hundred.</summary>
    public int Confidence { get; }

    /// <summary>Gets whether this candidate may proceed to the Risk Engine.</summary>
    public bool ExecutionEligible { get; }

    /// <summary>Gets the evidence and gating reasons supporting the decision.</summary>
    public IReadOnlyList<SignalReason> Reasons { get; }
}
