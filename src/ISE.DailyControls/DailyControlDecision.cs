using System;

namespace ISE.DailyControls;

/// <summary>Represents the account-level trading decision for the current moment.</summary>
public sealed class DailyControlDecision
{
    /// <summary>Creates a daily control decision.</summary>
    public DailyControlDecision(DailyControlAction action, DailyControlReason reason, decimal riskMultiplier)
    {
        if (riskMultiplier < 0 || riskMultiplier > 1) throw new ArgumentOutOfRangeException(nameof(riskMultiplier));
        Action = action;
        Reason = reason;
        RiskMultiplier = riskMultiplier;
    }

    /// <summary>Gets the selected control action.</summary>
    public DailyControlAction Action { get; }

    /// <summary>Gets the reason for the action.</summary>
    public DailyControlReason Reason { get; }

    /// <summary>Gets the permitted fraction of normal approved risk.</summary>
    public decimal RiskMultiplier { get; }

    /// <summary>Gets whether another trade may be initiated.</summary>
    public bool CanInitiateTrade => Action == DailyControlAction.AllowTrading || Action == DailyControlAction.ReduceRisk;
}
