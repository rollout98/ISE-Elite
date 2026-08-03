namespace ISE.Risk;

/// <summary>Defines configurable risk limits for one prop account.</summary>
public sealed class PropAccountProfile
{
    /// <summary>Creates a validated prop-account profile.</summary>
    public PropAccountProfile(string firm, AccountSize accountSize, int maximumContracts, decimal maximumRiskPerTrade, decimal dailyLossLimit, decimal drawdownLimit)
    {
        if (string.IsNullOrWhiteSpace(firm)) throw new ArgumentException("Firm is required.", nameof(firm));
        if (maximumContracts < 1) throw new ArgumentOutOfRangeException(nameof(maximumContracts));
        if (maximumRiskPerTrade <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRiskPerTrade));
        if (dailyLossLimit <= 0) throw new ArgumentOutOfRangeException(nameof(dailyLossLimit));
        if (drawdownLimit <= 0) throw new ArgumentOutOfRangeException(nameof(drawdownLimit));

        Firm = firm;
        AccountSize = accountSize;
        MaximumContracts = maximumContracts;
        MaximumRiskPerTrade = maximumRiskPerTrade;
        DailyLossLimit = dailyLossLimit;
        DrawdownLimit = drawdownLimit;
    }

    /// <summary>Gets the prop-firm name.</summary>
    public string Firm { get; }
    /// <summary>Gets the nominal account size.</summary>
    public AccountSize AccountSize { get; }
    /// <summary>Gets the absolute contract cap.</summary>
    public int MaximumContracts { get; }
    /// <summary>Gets the configured dollar risk cap per trade.</summary>
    public decimal MaximumRiskPerTrade { get; }
    /// <summary>Gets the configured daily loss limit.</summary>
    public decimal DailyLossLimit { get; }
    /// <summary>Gets the configured drawdown limit.</summary>
    public decimal DrawdownLimit { get; }
}
