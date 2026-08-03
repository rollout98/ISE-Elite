using System;

namespace ISE.Risk;

/// <summary>Represents the Risk Engine approval and contract quantity.</summary>
public sealed class RiskDecision
{
    /// <summary>Creates an immutable risk decision.</summary>
    public RiskDecision(bool approved, int contracts, decimal totalRisk, RiskDecisionReason reason)
    {
        if (contracts < 0) throw new ArgumentOutOfRangeException(nameof(contracts));
        if (totalRisk < 0) throw new ArgumentOutOfRangeException(nameof(totalRisk));
        Approved = approved;
        Contracts = contracts;
        TotalRisk = totalRisk;
        Reason = reason;
    }

    /// <summary>Gets whether the trade candidate passed risk approval.</summary>
    public bool Approved { get; }

    /// <summary>Gets the approved contract quantity.</summary>
    public int Contracts { get; }

    /// <summary>Gets the total planned dollar risk.</summary>
    public decimal TotalRisk { get; }

    /// <summary>Gets the approval or rejection reason.</summary>
    public RiskDecisionReason Reason { get; }
}
