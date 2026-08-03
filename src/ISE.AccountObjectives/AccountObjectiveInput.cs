using System;

namespace ISE.AccountObjectives;

/// <summary>Contains live account progress and upstream approvals.</summary>
public sealed class AccountObjectiveInput
{
    /// <summary>Creates validated objective input.</summary>
    public AccountObjectiveInput(AccountObjectiveProfile profile, decimal cumulativeProfit, decimal todayProfit, int completedTradingDays, bool strategyQualified, bool riskApproved, bool exceptionalSetup)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        if (cumulativeProfit < 0) throw new ArgumentOutOfRangeException(nameof(cumulativeProfit));
        if (todayProfit < 0) throw new ArgumentOutOfRangeException(nameof(todayProfit));
        if (completedTradingDays < 0) throw new ArgumentOutOfRangeException(nameof(completedTradingDays));
        CumulativeProfit = cumulativeProfit;
        TodayProfit = todayProfit;
        CompletedTradingDays = completedTradingDays;
        StrategyQualified = strategyQualified;
        RiskApproved = riskApproved;
        ExceptionalSetup = exceptionalSetup;
    }

    /// <summary>Gets the account objective profile.</summary>
    public AccountObjectiveProfile Profile { get; }
    /// <summary>Gets cumulative account profit toward the evaluation target.</summary>
    public decimal CumulativeProfit { get; }
    /// <summary>Gets today's realized profit.</summary>
    public decimal TodayProfit { get; }
    /// <summary>Gets completed qualifying trading days.</summary>
    public int CompletedTradingDays { get; }
    /// <summary>Gets whether the Strategy Engine qualified the trade.</summary>
    public bool StrategyQualified { get; }
    /// <summary>Gets whether the Risk Engine approved the trade.</summary>
    public bool RiskApproved { get; }
    /// <summary>Gets whether the setup is authorized as exceptional.</summary>
    public bool ExceptionalSetup { get; }
}
