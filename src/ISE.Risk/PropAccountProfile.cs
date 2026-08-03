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

    public string Firm { get; }
    public AccountSize AccountSize { get; }
    public int MaximumContracts { get; }
    public decimal MaximumRiskPerTrade { get; }
    public decimal DailyLossLimit { get; }
    public decimal DrawdownLimit { get; }
    public int MaximumTradesPerDay { get; }
}
