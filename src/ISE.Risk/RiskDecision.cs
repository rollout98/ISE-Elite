using System;

namespace ISE.Risk;

/// <summary>Represents the Risk Engine approval and contract quantity.</summary>
public sealed class RiskDecision
{
    public RiskDecision(bool approved, int contracts, decimal totalRisk, RiskDecisionReason reason)
    {
        if (contracts < 0) throw new ArgumentOutOfRangeException(nameof(contracts));
        if (totalRisk < 0) throw new ArgumentOutOfRangeException(nameof(totalRisk));
        Approved = approved;
        Contracts = contracts;
        TotalRisk = totalRisk;
        Reason = reason;
    }

    public bool Approved { get; }
    public int Contracts { get; }
    public decimal TotalRisk { get; }
    public RiskDecisionReason Reason { get; }
}
