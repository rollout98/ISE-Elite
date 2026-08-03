using System;

namespace ISE.Risk;

/// <summary>Contains account state and trade risk used for approval and sizing.</summary>
public sealed class RiskInput
{
    /// <summary>Creates a validated risk-evaluation input.</summary>
    public RiskInput(PropAccountProfile profile, bool signalEligible, decimal riskPerContract, decimal realizedDailyLoss, decimal remainingDrawdown, int tradesToday)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        if (riskPerContract <= 0) throw new ArgumentOutOfRangeException(nameof(riskPerContract));
        if (realizedDailyLoss < 0) throw new ArgumentOutOfRangeException(nameof(realizedDailyLoss));
        if (remainingDrawdown < 0) throw new ArgumentOutOfRangeException(nameof(remainingDrawdown));
        if (tradesToday < 0) throw new ArgumentOutOfRangeException(nameof(tradesToday));
        SignalEligible = signalEligible;
        RiskPerContract = riskPerContract;
        RealizedDailyLoss = realizedDailyLoss;
        RemainingDrawdown = remainingDrawdown;
        TradesToday = tradesToday;
    }

    /// <summary>Gets the configured prop-account profile.</summary>
    public PropAccountProfile Profile { get; }

    /// <summary>Gets whether the upstream signal is eligible for execution.</summary>
    public bool SignalEligible { get; }

    /// <summary>Gets the planned dollar risk for one contract.</summary>
    public decimal RiskPerContract { get; }

    /// <summary>Gets the realized loss for the current trading day.</summary>
    public decimal RealizedDailyLoss { get; }

    /// <summary>Gets the remaining drawdown capacity available to the account.</summary>
    public decimal RemainingDrawdown { get; }

    /// <summary>Gets the number of trades already taken during the current trading day.</summary>
    public int TradesToday { get; }
}
