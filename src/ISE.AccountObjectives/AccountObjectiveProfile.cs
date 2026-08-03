using System;

namespace ISE.AccountObjectives;

/// <summary>Defines firm rules and business objectives for one account.</summary>
public sealed class AccountObjectiveProfile
{
    /// <summary>Creates a validated account objective profile.</summary>
    public AccountObjectiveProfile(
        string firm,
        AccountStage stage,
        ObjectiveMode mode,
        decimal accountProfitTarget,
        int minimumTradingDays,
        int plannedPassDays,
        decimal preferredDailyProfit,
        decimal maximumDailyProfit,
        bool allowExceptionalExtension)
    {
        if (string.IsNullOrWhiteSpace(firm)) throw new ArgumentException("Firm is required.", nameof(firm));
        if (accountProfitTarget < 0) throw new ArgumentOutOfRangeException(nameof(accountProfitTarget));
        if (minimumTradingDays < 1) throw new ArgumentOutOfRangeException(nameof(minimumTradingDays));
        if (plannedPassDays < 1) throw new ArgumentOutOfRangeException(nameof(plannedPassDays));
        if (plannedPassDays < minimumTradingDays) throw new ArgumentException("Planned pass days cannot be less than minimum trading days.", nameof(plannedPassDays));
        if (preferredDailyProfit <= 0) throw new ArgumentOutOfRangeException(nameof(preferredDailyProfit));
        if (maximumDailyProfit < preferredDailyProfit) throw new ArgumentOutOfRangeException(nameof(maximumDailyProfit));
        if (stage == AccountStage.Evaluation && accountProfitTarget <= 0) throw new ArgumentOutOfRangeException(nameof(accountProfitTarget));

        Firm = firm;
        Stage = stage;
        Mode = mode;
        AccountProfitTarget = accountProfitTarget;
        MinimumTradingDays = minimumTradingDays;
        PlannedPassDays = plannedPassDays;
        PreferredDailyProfit = preferredDailyProfit;
        MaximumDailyProfit = maximumDailyProfit;
        AllowExceptionalExtension = allowExceptionalExtension;
    }

    /// <summary>Gets the prop firm or brokerage name.</summary>
    public string Firm { get; }
    /// <summary>Gets the account lifecycle stage.</summary>
    public AccountStage Stage { get; }
    /// <summary>Gets the assigned business objective.</summary>
    public ObjectiveMode Mode { get; }
    /// <summary>Gets the evaluation profit target; funded accounts may use zero.</summary>
    public decimal AccountProfitTarget { get; }
    /// <summary>Gets the firm's minimum required trading days.</summary>
    public int MinimumTradingDays { get; }
    /// <summary>Gets the planned number of days to complete the evaluation.</summary>
    public int PlannedPassDays { get; }
    /// <summary>Gets the preferred normal daily profit objective.</summary>
    public decimal PreferredDailyProfit { get; }
    /// <summary>Gets the maximum permitted daily profit objective.</summary>
    public decimal MaximumDailyProfit { get; }
    /// <summary>Gets whether exceptional setups may trade beyond the preferred daily objective.</summary>
    public bool AllowExceptionalExtension { get; }
}
