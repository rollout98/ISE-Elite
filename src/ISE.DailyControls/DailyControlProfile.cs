using System;

namespace ISE.DailyControls;

/// <summary>Defines configurable operating limits for one trading account.</summary>
public sealed class DailyControlProfile
{
    /// <summary>Creates a daily control profile.</summary>
    public DailyControlProfile(
        decimal preferredDailyProfit,
        decimal maximumDailyProfit,
        decimal dailyLossLimit,
        int maximumConsecutiveLosses,
        int maximumTradesPerDay,
        decimal reducedRiskMultiplier,
        bool allowExceptionalSetupsAfterTarget)
    {
        if (preferredDailyProfit < 0) throw new ArgumentOutOfRangeException(nameof(preferredDailyProfit));
        if (maximumDailyProfit <= 0 || maximumDailyProfit < preferredDailyProfit) throw new ArgumentOutOfRangeException(nameof(maximumDailyProfit));
        if (dailyLossLimit <= 0) throw new ArgumentOutOfRangeException(nameof(dailyLossLimit));
        if (maximumConsecutiveLosses <= 0) throw new ArgumentOutOfRangeException(nameof(maximumConsecutiveLosses));
        if (maximumTradesPerDay <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTradesPerDay));
        if (reducedRiskMultiplier <= 0 || reducedRiskMultiplier > 1) throw new ArgumentOutOfRangeException(nameof(reducedRiskMultiplier));

        PreferredDailyProfit = preferredDailyProfit;
        MaximumDailyProfit = maximumDailyProfit;
        DailyLossLimit = dailyLossLimit;
        MaximumConsecutiveLosses = maximumConsecutiveLosses;
        MaximumTradesPerDay = maximumTradesPerDay;
        ReducedRiskMultiplier = reducedRiskMultiplier;
        AllowExceptionalSetupsAfterTarget = allowExceptionalSetupsAfterTarget;
    }

    /// <summary>Gets the normal profit objective for the day.</summary>
    public decimal PreferredDailyProfit { get; }

    /// <summary>Gets the hard profit ceiling for the day.</summary>
    public decimal MaximumDailyProfit { get; }

    /// <summary>Gets the maximum permitted realized daily loss.</summary>
    public decimal DailyLossLimit { get; }

    /// <summary>Gets the maximum consecutive losing trades.</summary>
    public int MaximumConsecutiveLosses { get; }

    /// <summary>Gets the maximum trades allowed during the day.</summary>
    public int MaximumTradesPerDay { get; }

    /// <summary>Gets the risk multiplier used after the preferred target.</summary>
    public decimal ReducedRiskMultiplier { get; }

    /// <summary>Gets whether exceptional setups may continue after the preferred target.</summary>
    public bool AllowExceptionalSetupsAfterTarget { get; }
}
