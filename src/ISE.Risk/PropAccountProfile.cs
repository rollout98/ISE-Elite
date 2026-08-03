using System;

namespace ISE.Risk;

/// <summary>Defines configurable risk limits for one prop account.</summary>
public sealed class PropAccountProfile
{
    /// <summary>Creates a validated prop-account profile.</summary>
    public PropAccountProfile(string firm, AccountSize accountSize, int maximumContracts, decimal maximumRiskPerTrade, decimal dailyLossLimit, decimal drawdownLimit, int maximumTradesPerDay = 2)
    {
        if (string.IsNullOrWhiteSpace(firm)) throw new ArgumentException("Firm is required.", nameof(firm));
        if (maximumContracts < 1) throw new ArgumentOutOfRangeException(nameof(maximumContracts));
        if (maximumRiskPerTrade <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRiskPerTrade));
        if (dailyLossLimit <= 0) throw new ArgumentOutOfRangeException(nameof(dailyLossLimit));
        if (drawdownLimit <= 0) throw new ArgumentOutOfRangeException(nameof(drawdownLimit));
        if (maximumTradesPerDay < 1) throw new ArgumentOutOfRangeException(nameof(maximumTradesPerDay));

        Firm = firm;
        AccountSize = accountSize;
        MaximumContracts = maximumContracts;
        MaximumRiskPerTrade = maximumRiskPerTrade;
        DailyLossLimit = dailyLossLimit;
        DrawdownLimit = drawdownLimit;
        MaximumTradesPerDay = maximumTradesPerDay;
    }

    /// <summary>Gets the prop-firm name.</summary>
    public string Firm { get; }

    /// <summary>Gets the nominal account size.</summary>
    public AccountSize AccountSize { get; }

    /// <summary>Gets the absolute maximum number of contracts.</summary>
    public int MaximumContracts { get; }

    /// <summary>Gets the maximum permitted dollar risk for one trade.</summary>
    public decimal MaximumRiskPerTrade { get; }

    /// <summary>Gets the maximum permitted realized loss for one trading day.</summary>
    public decimal DailyLossLimit { get; }

    /// <summary>Gets the configured maximum drawdown allowance.</summary>
    public decimal DrawdownLimit { get; }

    /// <summary>Gets the maximum number of trades permitted per day.</summary>
    public int MaximumTradesPerDay { get; }
}
