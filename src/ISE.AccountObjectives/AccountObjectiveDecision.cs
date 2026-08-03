using System;

namespace ISE.AccountObjectives;

/// <summary>Represents the Account Objective Engine decision.</summary>
public sealed class AccountObjectiveDecision
{
    /// <summary>Creates a validated decision.</summary>
    public AccountObjectiveDecision(bool tradingPermitted, decimal dailyObjective, decimal dailyRemaining, decimal accountRemaining, ObjectiveDecisionReason reason)
    {
        if (dailyObjective < 0) throw new ArgumentOutOfRangeException(nameof(dailyObjective));
        if (dailyRemaining < 0) throw new ArgumentOutOfRangeException(nameof(dailyRemaining));
        if (accountRemaining < 0) throw new ArgumentOutOfRangeException(nameof(accountRemaining));
        TradingPermitted = tradingPermitted;
        DailyObjective = dailyObjective;
        DailyRemaining = dailyRemaining;
        AccountRemaining = accountRemaining;
        Reason = reason;
    }

    /// <summary>Gets whether another trade may be initiated.</summary>
    public bool TradingPermitted { get; }
    /// <summary>Gets today's calculated objective.</summary>
    public decimal DailyObjective { get; }
    /// <summary>Gets remaining profit toward today's objective.</summary>
    public decimal DailyRemaining { get; }
    /// <summary>Gets remaining profit toward the evaluation target.</summary>
    public decimal AccountRemaining { get; }
    /// <summary>Gets the explainable decision reason.</summary>
    public ObjectiveDecisionReason Reason { get; }
}
